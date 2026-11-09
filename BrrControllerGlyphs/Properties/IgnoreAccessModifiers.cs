// ------------------------------------------------------------------------------------------------
// IgnoreAccessModifiers.cs: lets the Mono JIT skip access checks for publicized game members.
// ------------------------------------------------------------------------------------------------

using System.Security.Permissions;

///////////////////////////////////////////////////////////////////////////////////////////////////

// From the Jotunn mod stub (originally Veilheim). We compile against publicized Valheim
// assemblies (private members made public), so at runtime the Mono JIT must skip access
// modifier checks. SecurityPermission is set to the minimum needed for that. Ticking
// "Unsafe code" in the project settings adds the same attribute; it's done here so the trick
// is explained in the code instead of hidden in a setting.

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
