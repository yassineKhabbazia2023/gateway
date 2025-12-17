// <copyright file="PennylaneAccessStatuses.cs" company="Pulse">
// Copyright (c) Pulse. All rights reserved.
// </copyright>

namespace ApiGateway.Pennylane.Models
{
    /// <summary>
    /// Canonical status values returned by Pennylane access grant operations.
    /// </summary>
    public static class PennylaneAccessStatuses
    {
        public const string Created = "created";
        public const string ExistingUserAccessGranted = "existing_user_access_granted";
        public const string AlreadyHasAccess = "already_has_access";
        public const string Failed = "failed";
    }
}
