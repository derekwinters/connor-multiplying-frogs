# The internet is permanently out; the local network is an open question

`CLAUDE.md` rule: *"No network calls, no telemetry, no analytics. The app is
fully offline."* Local-network play between two phones on the same wifi is
wanted for a later version, and it cannot honour the letter of that rule — LAN
discovery needs an Android network permission and sends real packets.

The decision is to **leave the rule intact for now and write the boundary down**.
v1 ships with **zero network permissions**, enforced in CI. The permanent line —
*no traffic ever leaves the local network, no servers, no third-party SDKs, no
telemetry* — is recorded in
[product scope](../specs/product-scope.md) so nobody designs local play out of the
architecture. The rule itself gets amended if and when local play is actually
built, as its own decision with a real feature in front of it.

## Why not just amend it now

Because the two formulations are not equally strong, and the difference is not a
formality.

**"Fully offline" is a checkable property.** CI can assert there is no network
permission in the Android manifest, and that assertion is mechanical — no future
agent can erode it, and no reviewer has to notice anything.

**"Only local traffic" is a promise.** The moment the app holds `INTERNET`
permission, no automated check can tell packets that stay on the subnet from
packets that don't. What protects the guarantee is everyone who touches the code
continuing to mean it.

Trading the first for the second may well be worth it to get local play. It is
not worth it eighteen months before local play exists, in exchange for nothing.

## Consequences

- A CI check asserting zero network permissions is v1 work, not a nice-to-have —
  it is the entire mechanism by which this decision holds.
- Local-network play is parked in
  [future ideas](../specs/future-ideas.md), explicitly flagged as requiring an
  override of a non-negotiable rule.
- The no-internet half of the line never moves, in any version. If a package
  wants internet access, it is the wrong package.
