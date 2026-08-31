// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Febris.UserNode.Portal.ViewModels
{
    /// <summary>
    /// Node-local curriculum authoring form.
    ///
    /// <para>
    /// Deliberately NOT the shared <c>CurriculumCreationViewModel</c>: 8 of that type's 12 members
    /// are hub-only (Industry, Category, Focus, Tag and their SelectLists, plus ContentDeveloper).
    /// That taxonomy is marketplace-scoped and deleted node-side, and no node user carries a
    /// ContentDeveloper claim. Binding to it would keep five dead overpost surfaces alive.
    /// </para>
    ///
    /// <para>
    /// This lives in the PORTAL rather than shared/FebrisModelLibrary on purpose. The shared
    /// library keeps the hub types for a future marketplace; keeping the node's authoring form
    /// here means it cannot quietly grow taxonomy fields back.
    /// </para>
    /// </summary>
    public class CurriculumAuthoringViewModel
    {
        public Curriculum Curriculum { get; set; }

        [Display(Name = "Classification of this Curriculum")]
        public Guid? SelectedCurriculumClassification { get; set; }

        /// <summary>
        /// Rebuilt by the controller on every render INCLUDING the invalid-ModelState re-render.
        /// It is not in the [Bind] list, so a form that posts back invalid would otherwise
        /// re-render with an empty dropdown.
        /// </summary>
        public SelectList CurriculumClassificationList { get; set; }
    }

    /// <summary>
    /// Backs the module-linking screen. Membership travels as a UUID set rather than a second
    /// entity list, so the checkbox markup can render checked state without a second lookup per row.
    /// </summary>
    public class CurriculumModuleLinkingViewModel
    {
        public Curriculum Curriculum { get; set; }

        /// <summary>Every module on the node -- the candidate set to link from.</summary>
        public List<Module> ModuleList { get; set; }

        /// <summary>UUIDs of the modules currently in this curriculum.</summary>
        public HashSet<Guid> LinkedModuleUuidList { get; set; }
    }
}
