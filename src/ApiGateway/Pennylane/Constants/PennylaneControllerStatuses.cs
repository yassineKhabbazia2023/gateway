namespace ApiGateway.Pennylane.Constants
{
    /// <summary>
    /// Canonical status values returned by the Pennylane controller.
    /// </summary>
    public static class PennylaneControllerStatuses
    {
        public const string Failed = "failed";
        public const string AlreadyHasAccess = "already_has_access";
        public const string Created = "created";
        public const string CreatedOnDefault = "created_on_default";
        public const string ExistingUserAccessGranted = "existing_user_access_granted";
        public const string UserNotFound = "user_not_found";
        public const string Revoked = "revoked";
        public const string AlreadyExists = "already_exists";
        public const string ToCreate = "to_create";
        public const string PartialSuccess = "partial_success";
        public const string Validated = "validated";
        public const string RequiresFirmAssignment = "requires_firm_assignment";
        public const string RequiresFirmTransfer = "requires_firm_transfer";
    }
}
