---
title: Licence
description: "The Memoria framework is free and open source under the Apache License 2.0. Memoria Web, the browser tool that reads a store, is a commercial product that is free for one service and paid above it. Which applies to you, and the full terms."
nav_order: 11
redirect_from:
  - /licence.html
  - /licensing.html
---

# Licence

Memoria is two things under two licences, and which one applies depends on which of them you are using.

The **framework** — every package published under the `Memoria` NuGet prefix — is free and open source under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). Use it in anything, commercial or not, closed source or open, at any scale, without asking and without paying. There is no edition, no threshold and no key.

**Memoria Web** — the browser tool that reads a store through your own domain assemblies — is a commercial product under the [Memoria Web Licence](#memoria-web-licence-agreement) below. Its source is published in the repository so you can read it, build it and audit what you are about to run, but running it is what the licence governs. It is **free for one service**, and paid above that.

## Which licence do you need?

| You are…                                                                              | Licence                              | Cost                                                                            |
|---------------------------------------------------------------------------------------|--------------------------------------|---------------------------------------------------------------------------------|
| Building software with the Memoria packages, however you licence what you build        | Apache License 2.0                   | Free                                                                             |
| Running Memoria Web over a single service                                              | Memoria Web Licence, **Community**   | Free, always                                                                     |
| Running Memoria Web over more than one service                                         | Memoria Web Licence, **Standard**, **Professional** or **Enterprise** | From $999 USD a year, see [the editions](#the-memoria-web-editions) |

A **service** is one entry in the `services` list of a `memoria.json` manifest installed in the tool: a named set of domain assemblies read over one connection string, browsed at its own address. Memoria Web counts them itself, so nothing here rests on your own assessment of your revenue, your headcount or your size.

Versions of the framework up to and including 1.9.1 were released under the Apache License 2.0 and remain under it. Version 2.0.0-beta, released on 17/09/2026, was briefly offered under the Reciprocal Public License 1.5 or a commercial licence covering the packages; that arrangement was withdrawn four days later at 2.0.0-beta.2 and the framework returned to Apache 2.0. Anyone who took 2.0.0-beta under either of those licences keeps them — a version is licensed under the terms it was released with — and Apache 2.0 grants strictly more, so there is nothing to do about it.

### Why the framework is free and the tool is not

A framework earns its keep by being adopted, and a licence that sends an evaluating engineer to their legal department before they can prototype is a tax on exactly the thing it needs most. Apache 2.0 removes that entirely and for good.

The tool is a different purchase. It is opened by a team already running event-sourced systems in production, to answer questions — what does this aggregate fold to, is its snapshot behind, which events produced this state — that otherwise cost an afternoon of hand-written queries each. One service covers every evaluation, every side project and most single-context shops, free and permanently. Beyond that, an organisation reading several stores through it is getting the kind of value that pays for the work.

### The Memoria Web editions

| Edition          | Services         | Scope                                                | Support                                        | Per year   | Per month   |
|------------------|------------------|------------------------------------------------------|------------------------------------------------|------------|-------------|
| **Community**    | 1                | One legal entity                                     | GitHub issues                                  | Free, and always will be | Free |
| **Standard**     | Up to 5          | One legal entity                                     | GitHub issues                                  | $999 USD   | $99.90 USD  |
| **Professional** | Up to 25         | One legal entity                                     | Email, first response within two business days | $2,499 USD | $249.90 USD |
| **Enterprise**   | Unlimited        | The Licensee and all its Affiliates                  | Email, first response within one business day  | $4,999 USD | $499.90 USD |

Every edition grants the same rights to the software itself: run Memoria Web against your stores, read its source, and modify it for your own use. The editions differ in how many services one instance may read, in whether affiliates are covered, and in the support that comes with them. There is no limit on how many people sign in, how many instances you run, or how many environments you run them in.

A paid edition is a subscription, paid yearly or monthly. Paid yearly, it runs for twelve months from purchase and renews for another twelve; paid monthly, it runs for a month and renews month by month, at a tenth of the yearly price, so a year paid monthly costs a fifth more than a year paid up front. Either way it covers every version of Memoria Web released while it runs, and renews at the price then published.

**Launch offer.** Any paid edition bought on or before 31 December 2026 is half price for its first term, yearly or monthly, and renews at that same half price for as long as the subscription is kept unbroken.

To license a paid edition, buy it on the [Pricing](pricing.html) page. To ask about any of this, or to arrange an Enterprise licence, a purchase order or a bank transfer, reach out via [LinkedIn](https://www.linkedin.com/in/lucabriguglia).

---

## Memoria Web Licence Agreement

This Memoria Web Licence Agreement (the "Agreement") is between Luca Cammarata Briguglia (the "Licensor") and the individual or legal entity that runs the Software under it (the "Licensee"). By running the Software, the Licensee agrees to these terms.

This Agreement governs Memoria Web alone. It does not govern the Memoria framework packages, which are licensed to everyone under the Apache License 2.0 and are unaffected by anything in this Agreement.

### 1. Definitions

1.1 "Software" means Memoria Web, in source and binary form, together with its documentation, and every update, upgrade, pre-release and modification of it that the Licensor makes available. It does not include any package published under the `Memoria` NuGet prefix.

1.2 "Affiliate" means any entity that controls, is controlled by, or is under common control with the Licensee, where "control" means the direct or indirect power to direct the management of the entity, whether by contract or otherwise, or ownership of fifty percent (50%) or more of its outstanding shares or beneficial ownership of it.

1.3 "Service" means one entry in the `services` list of a `memoria.json` manifest installed in the Software: a named set of domain assemblies read over one connection string, browsed at its own address. Two instances of the Software reading the same service count it once.

1.4 "Edition" means one of the Community, Standard, Professional or Enterprise editions described in section 3.

1.5 "Instance" means one running copy of the Software.

### 2. Grant of licence

2.1 Subject to this Agreement and, for a paid Edition, to payment of the applicable fees, the Licensor grants the Licensee a non-exclusive, non-transferable, worldwide licence, for the term of the Agreement and within the scope of the Licensee's Edition, to:

(a) run the Software, on any number of Instances, in any number of environments, accessed by any number of people;

(b) read, compile and build the Software from its published source;

(c) modify the Software for the Licensee's own internal use, and run the result under this Agreement; and

(d) make copies of the Software as reasonably required for backup, testing, continuous integration and deployment.

2.2 The number of Services the Software may read at once is set by the Licensee's Edition under section 3. No other use is metered.

### 3. Editions and scope

3.1 **Community.** Free of charge. One (1) Service, for one legal entity.

3.2 **Standard.** Up to five (5) Services, for one legal entity.

3.3 **Professional.** Up to twenty-five (25) Services, for one legal entity.

3.4 **Enterprise.** Unlimited Services, for the Licensee and all its Affiliates.

3.5 A Licensee whose use exceeds the scope of its Edition must license an Edition that covers it. The fees and support terms per Edition are those in section 6 as published at [lucabriguglia.github.io/Memoria/license.html](https://lucabriguglia.github.io/Memoria/license.html) on the day the Licensee accepts the Edition, and form part of this Agreement for that Edition.

3.6 The Community Edition will remain free of charge. The Licensor may revise the Service limits in this section for future versions of the Software, but never for a version already released.

### 4. Source code

4.1 The Licensor publishes the source code of the Software in the Memoria repository. Publishing it is not a grant of any licence beyond this Agreement: the Software is not open source, and the source is published so that the Licensee can read it, audit it, build it and modify it for its own use under section 2.1.

4.2 Reading the published source, and copying it as reasonably required to do so, requires no Edition and no fee.

4.3 A modification the Licensee makes under section 2.1(c) is the Licensee's own, subject to the Licensor's rights in the Software it is made to, and is governed by this Agreement when run.

### 5. Restrictions

The Licensee may not:

(a) distribute, sublicense, sell, rent, lease or host the Software, modified or not, for the use of anyone outside the Licensee and, under the Enterprise Edition, its Affiliates;

(b) remove, alter or circumvent any copyright, licence or attribution notice in the Software, or any mechanism in it that determines or reports the Edition in force or the number of Services in use;

(c) use the Licensor's name, or the Memoria name or logo, to endorse or promote a product of the Licensee's without the Licensor's prior written consent;

(d) run the Software outside the scope of the Licensee's Edition; or

(e) transfer or assign this Agreement, except to a successor of the whole of the Licensee's business with the Licensor's prior written consent, which will not be unreasonably withheld.

### 6. Fees

6.1 The Community Edition is free of charge.

6.2 A paid Edition is a subscription with a term of either twelve (12) months or one (1) month, chosen by the Licensee at purchase. Its fee per term is:

(a) Standard: $999 USD per twelve-month term, or $99.90 USD per one-month term;

(b) Professional: $2,499 USD per twelve-month term, or $249.90 USD per one-month term;

(c) Enterprise: $4,999 USD per twelve-month term, or $499.90 USD per one-month term.

6.3 Fees are payable in advance for the term and are non-refundable except where the law says otherwise. A subscription renews for a further term of the same length at the fee published for its Edition and term on the day of renewal, unless either party gives notice before the term ends. The Licensee may change from a one-month to a twelve-month term, or back, at a renewal.

6.4 A paid Edition bought on or before 31 December 2026 is charged at half the fee in 6.2 for its first term, and renews at that same half fee for every consecutive term the subscription is kept without a lapse. A lapsed subscription that is taken up again is charged the full fee.

6.5 Support is provided as follows, by email for the Professional and Enterprise Editions and through the GitHub issue tracker for the others. Professional: a first response within two business days. Enterprise: a first response within one business day. A response is an acknowledgement and a first assessment by the Licensor; it is not a guarantee of a fix or of a fix within any time. The Licensor may revise these support terms for future versions of the Software under section 13.2.

### 7. Term and termination

7.1 The Community Edition licence runs for as long as the Licensee complies with this Agreement. A paid Edition licence runs for the term the Licensee has paid for.

7.2 The Licensor may terminate this Agreement on written notice if the Licensee materially breaches it and does not cure the breach within thirty (30) days of being notified of it.

7.3 On termination, the Licensee must stop running the Software. Nothing in termination affects the Licensee's use of the Memoria framework packages, which are licensed separately under the Apache License 2.0, or the Licensee's own data in the stores the Software read.

7.4 A Licensee whose paid subscription lapses may continue under the Community Edition, within its one-Service limit, without further notice.

7.5 Sections 5, 8, 10, 11, 12 and 13 survive termination.

### 8. Intellectual property

The Software is licensed, not sold. The Licensor retains all right, title and interest in and to the Software, including all intellectual property rights. The Licensee owns its own modifications of the Software, subject to the Licensor's rights in the Software those modifications are made to.

### 9. Pre-release versions

An alpha, beta, preview or other pre-release version of the Software is licensed on the same terms as a release, and its use counts toward the scope of the Licensee's Edition in the same way. A pre-release may be incomplete, may change without notice before release, and is provided for evaluation and early adoption at the Licensee's own risk.

### 10. Third-party software

The Software depends on third-party packages, including the Memoria framework packages, that are licensed under their own terms. This Agreement does not modify those terms, and the Licensee's use of those packages is governed by them.

### 11. Disclaimer of warranty

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. THE LICENSOR DOES NOT WARRANT THAT THE SOFTWARE WILL BE ERROR-FREE OR THAT ITS OPERATION WILL BE UNINTERRUPTED.

### 12. Limitation of liability

TO THE MAXIMUM EXTENT PERMITTED BY LAW, THE LICENSOR SHALL NOT BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL OR PUNITIVE DAMAGES, OR FOR ANY LOSS OF PROFITS, REVENUE, DATA OR USE, ARISING OUT OF OR IN CONNECTION WITH THIS AGREEMENT OR THE SOFTWARE, HOWEVER CAUSED AND UNDER ANY THEORY OF LIABILITY. THE LICENSOR'S TOTAL LIABILITY UNDER THIS AGREEMENT SHALL NOT EXCEED THE FEES PAID BY THE LICENSEE FOR THE SOFTWARE IN THE TWELVE (12) MONTHS BEFORE THE EVENT GIVING RISE TO THE CLAIM, OR ONE HUNDRED US DOLLARS ($100 USD) FOR A LICENSEE THAT HAS PAID NONE. NOTHING IN THIS AGREEMENT EXCLUDES LIABILITY THAT CANNOT BE EXCLUDED BY LAW.

### 13. General

13.1 This Agreement is the entire agreement between the parties about the Software and supersedes every earlier understanding about it.

13.2 The Licensor may publish revised terms for future versions of the Software. Revised terms apply only to versions released after they are published; the terms a version was released with continue to govern that version.

13.3 If any provision of this Agreement is held unenforceable, the rest of it remains in force.

13.4 This Agreement is governed by the laws of England and Wales, and the courts of England and Wales have exclusive jurisdiction over any dispute arising out of it.

---

Copyright © Luca Cammarata Briguglia. All rights reserved.

## Related

- [Pricing](pricing.md) — the editions, and buying one
- [Memoria Web](tools/memoria-web.md) — what the tool does
- [Release notes](release-notes.md)
