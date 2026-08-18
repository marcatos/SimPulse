# ADR 0008 — Entitlement boundaries without StoreKit

- **Status:** ACCEPTED
- **Date:** 2026-08-18

## Context

Free / Premium / Pro tiers are product-defined. StoreKit must be introducible later without rewriting feature code.

## Decision

Put capability checks in `SimPulse.Domain.Entitlements.CapabilityGate` (pure functions of `ProductTier`).

Application code asks "may this tier export CSV?" not "is this the Pro SKU?".

A future `EntitlementStore` port on iOS will map StoreKit product IDs onto `ProductTier`. Until then, a development store can force a tier via settings (not shipped to App Store).

Do not implement purchases in Phase 0.

## Alternatives considered

- **Hard-code `#if PRO`:** Unshippable mix of builds.
- **Implement StoreKit now:** No app binary, no products, wasted surface.

## Consequences

- UI must be written to degrade when a capability is off.
- Bridge itself is a Pro capability; the Windows app may still run for development.

## Migration / reversal

Replacing the mapping is an iOS adapter change, not a domain change.
