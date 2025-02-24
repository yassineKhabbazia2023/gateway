using ApiGateway.Contact.Enum;

namespace ApiGateway.Contact.Models
{
    public class ContactMeViewModel
    {
        public ContactMeViewModel(int id, string? firstName, string? lastName, string? email, string? officePhone, string? officeMobile, string? oldId, string? type, Persona? persona)
        {
            this.Id = id;
            this.FirstName = firstName;
            this.LastName = lastName;
            this.Email = email;
            this.OfficePhone = officePhone;
            this.OfficeMobile = officeMobile;
            this.OldId = oldId;
            this.Persona = persona;
            if(type != null)
            {
                IsCustomer = (type == ContactType.Customer.ToString());
            }
        }

        public int Id { get; }

        public string? FirstName { get; }

        public string? LastName { get; }

        public string? Email { get; }

        public string? OfficePhone { get; }

        public string? OfficeMobile { get; }

        public bool? IsCustomer { get; }

        public string? OldId { get; }

        public Persona? Persona { get; }
    }
}
