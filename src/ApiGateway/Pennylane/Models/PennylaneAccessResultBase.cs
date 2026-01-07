// <copyright file="PennylaneAccessResultBase.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace ApiGateway.Pennylane.Models
{
    /// <summary>
    /// Base class for Pennylane access operation results.
    /// </summary>
    /// <typeparam name="T">The derived type for fluent method chaining.</typeparam>
    public abstract class PennylaneAccessResultBase<T>
        where T : PennylaneAccessResultBase<T>
    {
        /// <summary>
        /// Gets or sets the status of the operation.
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
        /// Gets or sets the error details if the operation failed.
        /// Null if Status is not "failed".
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the list of steps that were executed during the operation.
        /// </summary>
        [JsonPropertyName("steps")]
        public List<string>? Steps { get; set; }

        /// <summary>
        /// Sets the error and message, then returns this instance for fluent chaining.
        /// </summary>
        public T WithError(string error, string message)
        {
            Error = error;
            Message = message;
            return (T)this;
        }
    }
}
