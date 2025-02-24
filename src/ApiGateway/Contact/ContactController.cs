using ApiGateway.Contact.Exceptions;
using ApiGateway.Contact.Models;
using ApiGateway.Identity.context;
using ApiGateway.Identity.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Contact
{
    [Route("gtw/contact")]
    [ApiController]
    [Authorize]
    public class ContactController : ControllerBase
    {
        private readonly IUserContext userContext;
        private readonly IContactService contactService;

        public ContactController(IUserContext userContext, IContactService contactService)
        {
            this.contactService = contactService;
            this.userContext = userContext;
        }

        /// <summary>
        /// Retrieve current contact informations.
        /// </summary>
        /// <returns>Current contact informations</returns>
        /// <response code="200">Returns the current contact informations</response>
        /// <response code="404">If no contact has been found.</response>
        [HttpGet("me")]
        public async Task<ActionResult> Me()
        {
            var userEmail = userContext.User.GetEmail();

            Models.Contact? contact;

            try
            {
                contact = await contactService.GetContactAsync(userEmail);
            } 
            catch (ContactNotFoundException)
            {
                return this.NotFound();
            }

            if (contact == null)
            {
                return this.BadRequest();
            }

            ContactMeViewModel viewModelContactMe = new(contact.Id, contact.FirstName, contact.LastName, contact.Email, contact.LandPhone, contact.MobilePhone, contact.OldId, contact.Type, contact.Persona);
         
            return Ok(viewModelContactMe);
        }
    }
}
