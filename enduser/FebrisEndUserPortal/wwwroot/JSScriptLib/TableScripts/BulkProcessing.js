//connected to everything
$(document).ready(function () {

    //this means no button needs to be press to load the table
    $(document).on('keypress', 'textarea#jsonDataDump', function (e) {
        e.preventDefault();
        e.stopPropagation();
    });

    //no buttons are needed to after table is passed in
    $(document).on('keypress', 'textarea#excelPasteBox', function (e) {
        if (e.ctrlKey !== true && e.key !== 'v') {
            e.preventDefault();
            e.stopPropagation();
        }
    });

    //handles paste in box and building and filling table  --- does not work if there are internal cell spacings
    $(document).on('paste', 'textarea#excelPasteBox', function (e) {
        e.preventDefault();

        //var cb;
        //var clipText = ''; 
        var cb = e.originalEvent.clipboardData || window.clipboardData;
        var clipText = cb.getData('text');
        processPastedData(clipText);
        function processPastedData(data) {
            const pasteData = data;
            // Rows are split by a QUOTE-AWARE scan, not a regex.
            //
            // The previous regex was /\n(?=\b\w+\b\t)/g -- split at a newline only when the next
            // line begins with word characters followed by a tab. A roster whose identification
            // number column is BLANK starts its rows with a tab, so the lookahead failed and every
            // such row merged into the one above it. The whole paste collapsed into a single
            // malformed record while the UI still reported success.
            //
            // The lookahead existed for a real reason: a cell containing a line break must not be
            // treated as a row boundary. The naive fix (/\n(?=[^\n]*\t)/g, or a plain
            // split('\n')) trades one silent corruption for another -- measured, it splits a
            // quoted multi-line cell into two rows.
            //
            // Excel wraps any cell containing a newline or tab in double quotes, so tracking quote
            // state is what actually separates a row boundary from cell content. Handles a blank
            // first column, quoted multi-line cells, doubled "" escapes, and CRLF.
            const rows = splitPastedRows(pasteData);

            const table = document.getElementById("excelDataTable");
            table.innerHTML = '';
            for (let i = 0; i < rows.length; i++) {

                const cells = rows[i].split('\t');
                if (cells.length > 1) {
                    const row = document.createElement('tr');
                    for (let j = 0; j < cells.length; j++) {
                        const cell = document.createElement('td');
                        const cellText = document.createTextNode(cells[j]);
                        cell.appendChild(cellText);
                        row.appendChild(cell);
                    }
                    table.appendChild(row);
                }
            }
        }
    });
});

/**
 * Split pasted spreadsheet text into rows, honouring quoted cells.
 * A newline inside a quoted cell is content, not a row boundary.
 */
function splitPastedRows(data) {
    const rows = [];
    let current = '';
    let inQuotes = false;
    for (let i = 0; i < data.length; i++) {
        const ch = data[i];
        if (ch === '"') {
            // A doubled "" inside a quoted field is a literal quote, not a terminator.
            if (inQuotes && data[i + 1] === '"') { current += '""'; i++; continue; }
            inQuotes = !inQuotes;
            current += ch;
            continue;
        }
        if (ch === '\n' && !inQuotes) {
            rows.push(current.replace(/\r$/, ''));
            current = '';
            continue;
        }
        current += ch;
    }
    if (current.length > 0) { rows.push(current.replace(/\r$/, '')); }
    return rows;
}
