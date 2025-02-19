using ApiGateway.Contact.Models;

namespace ApiGateway.UnitTests.Contact.Models
{
    public class ContactMeViewModelTests
    {
        [Fact]
        public void ContactMeViewModel_WhenTypeIsCustomer()
        {
            var contact = new ContactMeViewModel(1, "John", "Doe", "jdoe@test.fr", "123", "456", "00000000-0000-0000-0000-000000000000", "Customer");

            contact.Id.Should().Be(1);
            contact.Email.Should().Be("jdoe@test.fr");
            contact.FirstName.Should().Be("John");
            contact.LastName.Should().Be("Doe");
            contact.OfficePhone.Should().Be("123");
            contact.OfficeMobile.Should().Be("456");
            contact.IsCustomer.Should().BeTrue();
        }

        [Fact]
        public void ContactMeViewModel_WhenTypeIsCollab()
        {
            var contact = new ContactMeViewModel(1, "John", "Doe", "jdoe@test.fr", "123", "456", "00000000-0000-0000-0000-000000000000", "Collaborator");

            contact.IsCustomer.Should().BeFalse();
        }
    }
}
