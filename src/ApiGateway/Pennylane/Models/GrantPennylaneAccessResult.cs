// <copyright file="GrantPennylaneAccessResult.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace ApiGateway.Pennylane.Models
{
    /// <summary>
    /// Result of granting Pennylane access to a user.
    /// </summary>
    public class GrantPennylaneAccessResult
    {
        /// <summary>
        /// Gets or sets the status of the operation.
        /// - "created": User was newly created and access granted
        /// - "existing_user_access_granted": Existing user was granted access to the company
        /// - "already_has_access": User already had access to the company
        /// - "failed": Operation failed
        /// </summary>
        [JsonPropertyName("status")]
        public required string Status { get; set; }

        /// <summary>
        /// Gets or sets a user-friendly message describing the result.
        /// </summary>
        [JsonPropertyName("message")]
        public required string Message { get; set; }

        /// <summary>
        /// Gets or sets the contact ID.
        /// </summary>
        [JsonPropertyName("contactId")]
        public int ContactId { get; set; }

        /// <summary>
        /// Gets or sets the account ID.
        /// </summary>
        [JsonPropertyName("accountId")]
        public int AccountId { get; set; }

        /// <summary>
        /// Gets or sets the Pennylane user ID if available.
        /// </summary>
        [JsonPropertyName("pennylaneUserId")]
        public string? PennylaneUserId { get; set; }

        /// <summary>
        /// Gets or sets the Pennylane company ID if available.
        /// </summary>
        [JsonPropertyName("pennylaneCompanyId")]
        public string? PennylaneCompanyId { get; set; }

        /// <summary>
        /// Gets or sets the role assigned to the user.
        /// </summary>
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        /// <summary>
        /// Gets or sets the error details if the operation failed.
        /// Null if Status is not "failed".
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the list of steps that were executed during the operation.
        /// Each step describes what happened (e.g., "Company found", "User created").
        /// </summary>
        [JsonPropertyName("steps")]
        public List<string>? Steps { get; set; }

        /// <summary>
        /// Sets the error and message, then returns this instance for fluent chaining.
        /// </summary>
        public GrantPennylaneAccessResult WithError(string error, string message)
        {
            Error = error;
            Message = message;
            return this;
        }
    }
}
