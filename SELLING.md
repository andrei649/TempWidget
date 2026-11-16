# Selling TempWidget: what the research found

Gathered on 16 November 2026 by three research passes over public pages.
Items marked "unconfirmed" could not be verified on a primary source; check
them before relying on them. None of this is legal or tax advice.

## The $1 price

A $1 price mostly pays the payment processor.

| Where | Fee on a $1.00 sale | You keep (approx.) | Handles EU VAT for you |
|---|---|---|---|
| itch.io | your chosen share (default 10%) + about $0.30 + 2.9% | $0.57 | Only in its "Payouts" mode; $5 payout minimum |
| Paddle | 5% + $0.50 | $0.45 | Yes; asks you to contact them for products under $10 |
| Lemon Squeezy | 5% + $0.50, more for international cards | $0.43 or less | Yes |
| Gumroad | 10% + $0.50 | $0.40 | Yes |
| Stripe payment link | about $0.30 + 2.9% | $0.65 or less | No, you file VAT yourself |

The arithmetic is mine from the listed fees and was not double-checked.
At $3 the same fixed fees leave roughly $2.20 to $2.40, about five times
as much per sale. "Pay what you want, minimum $1" (Gumroad and itch.io both
offer it) keeps the $1 headline while letting people pay more.

## Microsoft Store

- The Store's own payment system appears to be for MSIX-packaged apps only.
  The installer (EXE/MSI) route looks like a free listing that has to use
  its own payment method. Unconfirmed: no page says so outright; confirm in
  Partner Center before planning around it.
- An MSIX-packaged TempWidget cannot ask for administrator rights unless
  Microsoft approves a capability it normally refuses, so a paid Store
  edition would be the limited one without CPU temperature.
- Individual developer registration is free through
  storedeveloper.microsoft.com (ID check required).
- Unconfirmed: the lowest price tier and Microsoft's revenue share.

## Before charging money

1. **PawnIO permission.** The driver is GPL-2.0-or-later with an exception
   for programs that talk to it through its control interface. The installer
   repository has no licence file. Selling a product that bundles the
   official installer is a question for the author (admin@namazso.eu), who
   also offers commercial licensing. Get the answer in writing.
2. **Open-source notices.** Selling a closed-source app that uses unmodified
   MPL-2.0, Apache-2.0 and MIT libraries is allowed as long as the notices
   and licence texts ship with it and the MPL source is reachable. That is
   what THIRD-PARTY-NOTICES.txt does.
3. **Code signing.** Unsigned downloads show a SmartScreen warning, and paid
   software that asks for administrator rights and installs a driver will be
   treated with suspicion. Microsoft's cheap signing service is not open to
   individuals in the EU. A standard individual certificate from Certum is
   roughly $139 a year (reseller price, unconfirmed). Open-source
   certificates may not be used for paid software.
4. **Taxes.** Selling to consumers in the EU means VAT. A merchant-of-record
   seller (Gumroad, Lemon Squeezy, Paddle) handles it; a plain Stripe link
   does not.

## A practical path

1. Finish testing on your laptop and on one desktop PC.
2. Ask the PawnIO author about bundling.
3. Sell the installer on Gumroad or itch.io as "pay what you want, from $1",
   or at a flat $3.
4. Add a signing certificate once sales cover it; until then, explain the
   SmartScreen warning on the product page.
5. Treat the Microsoft Store as a later, free-listing step for visibility.

## What the market research says people want

Built in version 2.1 because they came up most and fit a light widget:
network speed, a choice of tray reading (CPU or GPU), per-core and drive
health on hover, lowest/average/highest, CSV logging.

Deliberately left out:
- **Taskbar-embedded readout.** Popular, but the tool best known for it has
  a long list of breakages after Windows updates.
- **In-game overlay.** Established free tools own this.
- **Fan control.** A wrong fan curve can overheat hardware.
- **Web/remote endpoint, accounts, telemetry.** They would end the
  "no network connections" promise, which is a selling point.

Selling points the research supports: small and light, no ads, no account,
no telemetry, and none of the antivirus trouble caused by the old WinRing0
driver that several free monitors used (TempWidget uses PawnIO instead).
I found no direct evidence that people will pay for a sensor widget; free
tools dominate this category.

## Sources

- https://learn.microsoft.com/en-us/windows/apps/publish/store-policies
- https://learn.microsoft.com/en-us/windows/apps/publish/get-started
- https://learn.microsoft.com/en-us/windows/apps/publish/whats-new-individual-developer
- https://gumroad.com/pricing
- https://docs.lemonsqueezy.com/help/getting-started/fees
- https://www.paddle.com/pricing
- https://itch.io/docs/creators/payments
- https://stripe.com/en-ro/pricing
- https://github.com/namazso/PawnIO
- https://www.mozilla.org/en-US/MPL/2.0/FAQ/
- https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart
- https://github.com/zhongyang219/TrafficMonitor/blob/master/README_en-us.md
- https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/issues/1660
- https://support.microsoft.com/en-us/windows/security/threat-malware-protection/microsoft-defender-antivirus-alert-vulnerabledriver-winnt-winring0
- https://nzxt.com/pages/cam
- https://www.hwinfo.com/add-ons/
