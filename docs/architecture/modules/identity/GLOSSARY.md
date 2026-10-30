# Identity & Organization

The platform's people, authentication, workspace participation and administrative authority. Workspace is the product-facing name for Organization.

## People and authentication

**User**:
A globally identified person who can authenticate and participate in zero or more organizations.
_Avoid_: Tenant user, organization account when referring to the person.

**Credential**:
Evidence used to establish control of a User identity, such as a password.

**Authentication session**:
An established authenticated relationship between a User and the platform, with its own validity and revocation.

**Recipient verification**:
The invitation-specific proof supplied by a valid single-use invitation link and sign-in with the matching invited email.

## Workspaces and participation

**Organization**:
The customer workspace that owns projects and governs its membership and configuration. Workspace is its product-facing synonym; tenant describes its isolation boundary.
_Avoid_: Project, client company as interchangeable ownership concepts.

**Organization membership**:
A person's durable participation relationship with one organization, including its current state and retained history.
_Avoid_: Role, invitation.

**Owner**:
A protected organization responsibility held by one or more eligible members, with continuity required during normal membership administration.
_Avoid_: System Administrator, project owner as synonyms.

**Organization Admin**:
An organization-scoped administrative role whose authority is defined by its permitted operations.

**Invitation**:
A time-bounded offer for an intended recipient to join an organization under specified terms.
_Avoid_: Membership, access grant before acceptance.

**Guest**:
A person admitted to selected projects or shared resources without requiring general organization membership.
_Avoid_: Organization Member as a mandatory classification for all external participants.

## Authority

**Role**:
A named collection of permissions intended for a particular authorization scope.
_Avoid_: Job title, responsibility.

**Permission**:
Authority to attempt a defined action within its allowed scope, subject to the resource's applicable policies.

**Role assignment**:
The association that gives an eligible person a role within its intended scope and validity.

**Built-in role**:
A system-controlled role whose definition customers cannot edit, such as organization Owner, Admin or Member.

**Custom role**:
An organization-defined collection of permitted powers, created alongside protected built-in roles.

**Delegation authority**:
Authority to grant specified powers to another person; it is distinct from being permitted to perform the underlying action.

**System Administrator**:
A person with system-controlled platform authority that is independent of customer organization membership.
_Avoid_: Organization Owner, unrestricted customer administrator.

**Privileged activation**:
A bounded period in which a platform operator satisfies the additional conditions required to exercise sensitive authority.

**Revocation**:
Termination of future authority while preserving attribution of previous actions.
_Avoid_: Erasure, deletion of history.
