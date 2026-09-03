using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.UserNode.Portal.Controllers.User;
using Febris.UserNode.Portal.IdentityPolicy;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the role a user-creation page opens with (2026-09-02).
    ///
    /// <para>
    /// THE DEFECT THIS CLOSES. Four navbar lists (Comprehensive, Learners, Educators, Admins) all
    /// funnel into ONE add action, <c>User/Create</c>, and the anchor that reaches it carried no
    /// route value. So an operator who opened the Educators list and pressed Add landed on a page
    /// whose role dropdown was blank, and had to re-state the role the list had already implied.
    /// </para>
    ///
    /// <para>
    /// The null case matters as much as the seeded one. The Comprehensive list has no single role to
    /// carry, and every caller behaved that way before this change, so a bare Create must still hand
    /// the view a null model rather than one defaulted to a role nobody chose.
    /// </para>
    ///
    /// <para>
    /// WHY NO RANK ASSERTION HERE. The controller deliberately does not consult RoleRankPolicy. The
    /// view computes the assignable set to build the dropdown and seeds the selection only when the
    /// requested role is in it, so the guard lives with the data it guards. UserLogic.Create enforces
    /// CanAssign again on POST, so this action is UX only and widens nothing.
    /// </para>
    /// </summary>
    public class AddUserRolePreselectionTests
    {
        private static Mock<UserManager<LocalApplicationUser>> MockUserManager()
        {
            Mock<IUserStore<LocalApplicationUser>> store = new Mock<IUserStore<LocalApplicationUser>>();
            return new Mock<UserManager<LocalApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private static UserController BuildController()
        {
            return new UserController(
                Mock.Of<IUserLogic>(),
                NullLogger<UserController>.Instance,
                MockUserManager().Object,
                new ConfigurationBuilder().Build(),
                Mock.Of<ICohortMemberLogic>(),
                Mock.Of<ICsvUserImporter>());
        }

        [Fact]
        public void Create_WithNoAccountType_PassesNoModel_SoTheDropdownStaysUnchosen()
        {
            ViewResult result = BuildController().Create((InstitutionUserAccountType?)null) as ViewResult;

            result.Should().NotBeNull();
            result.Model.Should().BeNull(
                "the Comprehensive list implies no single role, and this is the pre-change behaviour");
        }

        // The three lists that DO imply a role, and the enum member each carries. These match the
        // filters the list partials already apply at UserController Get(User), Get(Educator) and
        // Get(Admin), so a mismatch here means the Add button disagrees with the list beneath it.
        [Theory]
        [InlineData(InstitutionUserAccountType.User)]
        [InlineData(InstitutionUserAccountType.Educator)]
        [InlineData(InstitutionUserAccountType.Admin)]
        public void Create_WithAnAccountType_SeedsItOntoTheModel(InstitutionUserAccountType requested)
        {
            ViewResult result = BuildController().Create(requested) as ViewResult;

            result.Should().NotBeNull();
            LocalUserCreation model = result.Model as LocalUserCreation;
            model.Should().NotBeNull("a requested role must reach the view to be pre-selected");
            model.UserAccountType.Should().Be(requested);
        }

        /// <summary>
        /// The enum carries explicit values starting at 101, so default(InstitutionUserAccountType)
        /// is 0 and matches no member. That is what makes the null-model path safe: a bare model
        /// would otherwise have silently meant "User" and pre-selected a role nobody asked for.
        /// </summary>
        [Fact]
        public void The_enum_has_no_zero_member_so_a_default_model_could_not_masquerade_as_a_role()
        {
            ((int)InstitutionUserAccountType.User).Should().Be(101);
            System.Enum.IsDefined(typeof(InstitutionUserAccountType), 0).Should().BeFalse();
        }
    }
}
