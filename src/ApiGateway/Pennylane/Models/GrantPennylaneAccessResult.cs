// <copyright file="GrantPennylaneAccessResult.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace ApiGateway.Pennylane.Models
{
    /// <summary>
    /// Result of granting Pennylane access to a user.
    /// </summary>
    public class GrantPennylaneAccessResult : PennylaneAccessResultBase<GrantPennylaneAccessResult>
    {
        /// <summary>
        /// Gets or sets the role assigned to the user.
        /// </summary>
        [JsonPropertyName("role")]
        public string? Role { get; set; }
    }
}
