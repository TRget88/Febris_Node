// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Febris.ModelLibrary.ViewModels;

namespace Febris.UserNode.Portal.IdentityPolicy
{
    /// <summary>
    /// Parses an uploaded CSV file into a <see cref="BulkUserCreationSubmitListViewModel"/>.
    /// The parser is intentionally forgiving: it never throws on malformed input, instead
    /// collecting a human-readable error string for every row it has to reject.
    /// </summary>
    public interface ICsvUserImporter
    {
        (BulkUserCreationSubmitListViewModel Model, System.Collections.Generic.IReadOnlyList<string> Errors)
            Parse(System.IO.Stream csvStream);
    }

    public sealed class CsvUserImporter : ICsvUserImporter
    {
        // Logical columns we know how to populate on BulkUserCreationSubmitViewModel.
        private enum Column
        {
            None,
            IdentificationNumber,
            FirstName,
            LastName,
            EmailAddress,
            PhoneNumber
        }

        // Normalized header token -> logical column. Normalization strips everything that
        // isn't a letter or digit and lower-cases, so "First Name", "first_name" and
        // "FIRSTNAME" all collapse to the same key.
        private static readonly Dictionary<string, Column> HeaderAliases =
            new Dictionary<string, Column>(StringComparer.Ordinal)
            {
                // Identification number
                { "identificationnumber", Column.IdentificationNumber },
                { "identification", Column.IdentificationNumber },
                { "idnumber", Column.IdentificationNumber },
                { "id", Column.IdentificationNumber },
                { "studentid", Column.IdentificationNumber },
                { "studentnumber", Column.IdentificationNumber },
                { "employeeid", Column.IdentificationNumber },
                { "nationalid", Column.IdentificationNumber },

                // First name
                { "firstname", Column.FirstName },
                { "first", Column.FirstName },
                { "givenname", Column.FirstName },
                { "forename", Column.FirstName },

                // Last name
                { "lastname", Column.LastName },
                { "last", Column.LastName },
                { "surname", Column.LastName },
                { "familyname", Column.LastName },

                // Email
                { "emailaddress", Column.EmailAddress },
                { "email", Column.EmailAddress },
                { "emailaddr", Column.EmailAddress },
                { "mail", Column.EmailAddress },
                { "emailid", Column.EmailAddress },

                // Phone
                { "phonenumber", Column.PhoneNumber },
                { "phone", Column.PhoneNumber },
                { "mobile", Column.PhoneNumber },
                { "mobilenumber", Column.PhoneNumber },
                { "cell", Column.PhoneNumber },
                { "cellphone", Column.PhoneNumber },
                { "telephone", Column.PhoneNumber },
                { "tel", Column.PhoneNumber },
                { "contactnumber", Column.PhoneNumber },
                { "contact", Column.PhoneNumber },
            };

        public (BulkUserCreationSubmitListViewModel Model, System.Collections.Generic.IReadOnlyList<string> Errors)
            Parse(System.IO.Stream csvStream)
        {
            var model = new BulkUserCreationSubmitListViewModel
            {
                SubmissionList = new List<BulkUserCreationSubmitViewModel>()
            };
            var errors = new List<string>();

            try
            {
                if (csvStream == null)
                {
                    errors.Add("No CSV file was provided.");
                    return (model, errors);
                }

                string text = ReadAll(csvStream);
                List<List<string>> records = Tokenize(text);

                // Locate the header: the first record that is not entirely blank.
                int headerIndex = -1;
                for (int r = 0; r < records.Count; r++)
                {
                    if (!IsBlankRecord(records[r]))
                    {
                        headerIndex = r;
                        break;
                    }
                }

                if (headerIndex < 0)
                {
                    errors.Add("The CSV file is empty or contains no header row.");
                    return (model, errors);
                }

                // Map each physical column index to a logical column.
                List<string> header = records[headerIndex];
                var columnMap = new Column[header.Count];
                bool emailColumnPresent = false;
                for (int c = 0; c < header.Count; c++)
                {
                    string key = NormalizeHeader(header[c]);
                    Column column;
                    columnMap[c] = HeaderAliases.TryGetValue(key, out column) ? column : Column.None;
                    if (columnMap[c] == Column.EmailAddress)
                    {
                        emailColumnPresent = true;
                    }
                }

                if (!emailColumnPresent)
                {
                    errors.Add("The CSV header has no recognizable email column (expected a column such as \"Email\" or \"EmailAddress\"). No users were imported.");
                    return (model, errors);
                }

                // Track duplicate emails case-insensitively.
                var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int dataRowNumber = 0;

                for (int r = headerIndex + 1; r < records.Count; r++)
                {
                    List<string> record = records[r];

                    // Silently skip blank lines.
                    if (IsBlankRecord(record))
                    {
                        continue;
                    }

                    dataRowNumber++;

                    var row = new BulkUserCreationSubmitViewModel();
                    for (int c = 0; c < record.Count && c < columnMap.Length; c++)
                    {
                        string value = (record[c] ?? string.Empty).Trim();
                        switch (columnMap[c])
                        {
                            case Column.IdentificationNumber:
                                row.IdentificationNumber = value;
                                break;
                            case Column.FirstName:
                                row.FirstName = value;
                                break;
                            case Column.LastName:
                                row.LastName = value;
                                break;
                            case Column.EmailAddress:
                                row.EmailAddress = value;
                                break;
                            case Column.PhoneNumber:
                                row.PhoneNumber = value;
                                break;
                        }
                    }

                    string email = row.EmailAddress == null ? string.Empty : row.EmailAddress.Trim();

                    if (string.IsNullOrWhiteSpace(email))
                    {
                        errors.Add("Row " + dataRowNumber + ": skipped because it has no email address.");
                        continue;
                    }

                    if (!seenEmails.Add(email))
                    {
                        errors.Add("Row " + dataRowNumber + ": skipped because of a duplicate email address '" + email + "'.");
                        continue;
                    }

                    row.EmailAddress = email;
                    model.SubmissionList.Add(row);
                }
            }
            catch (Exception ex)
            {
                // Contract: never throw. Surface the failure as an error string instead.
                errors.Add("Could not read the CSV file: " + ex.Message);
            }

            return (model, errors);
        }

        private static string ReadAll(Stream csvStream)
        {
            // Do not dispose the caller's stream; honor its BOM if present.
            using (var reader = new StreamReader(
                csvStream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// RFC-4180-ish tokenizer. Splits the text into records of fields, honoring
        /// double-quoted fields (which may contain commas, CR/LF, and escaped "" quotes).
        /// </summary>
        private static List<List<string>> Tokenize(string text)
        {
            var records = new List<List<string>>();
            var current = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            int i = 0;
            int n = text == null ? 0 : text.Length;

            while (i < n)
            {
                char ch = text[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < n && text[i + 1] == '"')
                        {
                            field.Append('"'); // escaped quote
                            i += 2;
                            continue;
                        }
                        inQuotes = false;
                        i++;
                        continue;
                    }

                    field.Append(ch);
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = true;
                    i++;
                    continue;
                }

                if (ch == ',')
                {
                    current.Add(field.ToString());
                    field.Clear();
                    i++;
                    continue;
                }

                if (ch == '\r' || ch == '\n')
                {
                    current.Add(field.ToString());
                    field.Clear();
                    records.Add(current);
                    current = new List<string>();
                    // Collapse a CRLF pair into a single record break.
                    if (ch == '\r' && i + 1 < n && text[i + 1] == '\n')
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                    continue;
                }

                field.Append(ch);
                i++;
            }

            // Flush the trailing field/record (a file that ends with a newline yields a
            // final blank record here, which IsBlankRecord filters out downstream).
            current.Add(field.ToString());
            records.Add(current);

            return records;
        }

        private static bool IsBlankRecord(List<string> record)
        {
            if (record == null || record.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < record.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(record[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(header.Length);
            foreach (char ch in header)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }

            return sb.ToString();
        }
    }
}
