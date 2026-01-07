// <copyright file="RevokeAccessRequest.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace ApiGateway.Pennylane.Models
{
    /// <summary>
    /// Request to revoke Pennylane access from a user.
    /// </summary>
    public class RevokeAccessRequest
    {
        /// <summary>
        /// Gets or sets the contact ID.
        /// </summary>
        public int ContactId { get; set; }

        /// <summary>
        /// Gets or sets the account ID.
        /// </summary>
        public int AccountId { get; set; }
    }
}
