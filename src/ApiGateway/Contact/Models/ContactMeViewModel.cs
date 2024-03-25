namespace ApiGateway.Contact.Models
{
    public class ContactMeViewModel
    {
        public ContactMeViewModel(int id, string? firstName, string? lastName, string? email, string? officePhone, string? officeMobile)
        {
            this.Id = id;
            this.FirstName = firstName;
            this.LastName = lastName;
            this.Email = email;
            this.OfficePhone = officePhone;
            this.OfficeMobile = officeMobile;
        }

        public int Id { get; }
        public string? FirstName { get; }
        public string? LastName { get; }
        public string? Email { get; }
        public string? OfficePhone { get; }
        public string? OfficeMobile { get; }
    }
}
