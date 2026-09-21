---
title: Pricing
description: "The Memoria framework is free under Apache 2.0. Memoria Web is free for one service and paid above it — what each edition costs, and how to buy one."
nav_order: 10
# Hidden from the nav and from search while the checkout is wired to the Paddle *sandbox*: the buy
# buttons open a checkout that looks real and takes test cards only. Drop both lines the moment the
# live catalogue exists and MEMORIA_PADDLE below is switched to production.
nav_exclude: true
search_exclude: true
---

# Pricing
{: .no_toc }

**The framework costs nothing, ever.** Every package under the `Memoria` NuGet prefix is under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). Nothing on this page applies to it.

This page is the shop for [Memoria Web](tools/memoria-web.html), the browser tool that reads a store through your own domain assemblies. The [Licence](license.html) page is the contract — the full terms, and the fees as published there form part of the agreement for the edition you accept.

1. TOC
{:toc}

## What you pay nothing for

**The framework, in full.** Build whatever you like with the packages, licence that however you like, ship it to whomever you like, at any scale. No edition, no threshold, no key, nothing to sign, nothing to assess. That is what Apache 2.0 means and it is not going to change.

**Memoria Web, over a single service.** A *service* is one entry in the `services` list of a `memoria.json` manifest installed in the tool: a named set of domain assemblies read over one connection string, browsed at its own address. One of those is free, permanently, with no limit on how many people sign in, how many instances you run or how many environments you run them in.

That covers every evaluation, every side project and most single-context shops. You pay when one instance reads a second store — which is to say, when you have enough event-sourced systems in production for the tool to be saving you real time.

There is nothing to apply for and no eligibility to assess. Your Settings page lists the services each installed archive declares, so counting them is a glance.

## Paid editions

Every edition grants the same rights to the software: run Memoria Web against your stores, read its source, and modify it for your own use. The editions differ in how many services one instance may read, in whether affiliates are covered, and in the support that comes with them.

<div class="term-toggle" role="group" aria-label="Billing term">
  <button type="button" class="btn term-option" data-term="year" aria-pressed="true">Yearly</button>
  <button type="button" class="btn term-option" data-term="month" aria-pressed="false">Monthly</button>
</div>

<table class="pricing-table">
  <thead>
    <tr>
      <th scope="col">Edition</th>
      <th scope="col">Services</th>
      <th scope="col">Support</th>
      <th scope="col">Price</th>
      <th scope="col"></th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <th scope="row">Community</th>
      <td>1, for one legal entity</td>
      <td>GitHub issues</td>
      <td class="price">
        <span class="price-launch">Free</span>
        <span class="price-term">and always will be</span>
      </td>
      <td><a class="btn" href="tools/memoria-web.html">Start reading</a></td>
    </tr>
    <tr>
      <th scope="row">Standard</th>
      <td>Up to 5, for one legal entity</td>
      <td>GitHub issues</td>
      <td class="price"
          data-launch-year="$499.50" data-list-year="$999" data-term-year="a year"
          data-launch-month="$49.95" data-list-month="$99.90" data-term-month="a month">
        <span class="price-launch">$499.50</span>
        <s class="price-list">$999</s>
        <span class="price-term">a year</span>
      </td>
      <td>
        <button type="button" class="btn btn-primary buy"
                data-edition="standard"
                data-price-year="pri_01m31zkmd5cbgr73v8zmdezavg"
                data-price-month="pri_01m31zkmjvf348azmss5vxyvpv">Buy Standard</button>
      </td>
    </tr>
    <tr>
      <th scope="row">Professional</th>
      <td>Up to 25, for one legal entity</td>
      <td>Email, first response within two business days</td>
      <td class="price"
          data-launch-year="$1,249.50" data-list-year="$2,499" data-term-year="a year"
          data-launch-month="$124.95" data-list-month="$249.90" data-term-month="a month">
        <span class="price-launch">$1,249.50</span>
        <s class="price-list">$2,499</s>
        <span class="price-term">a year</span>
      </td>
      <td>
        <button type="button" class="btn btn-primary buy"
                data-edition="professional"
                data-price-year="pri_01m31zkmx4bwypze9pzsfj8hmp"
                data-price-month="pri_01m31zkn1jqtdz3bthgcg5xf31">Buy Professional</button>
      </td>
    </tr>
    <tr>
      <th scope="row">Enterprise</th>
      <td>Unlimited, for you and all your affiliates</td>
      <td>Email, first response within one business day</td>
      <td class="price"
          data-launch-year="$2,499.50" data-list-year="$4,999" data-term-year="a year"
          data-launch-month="$249.95" data-list-month="$499.90" data-term-month="a month">
        <span class="price-launch">$2,499.50</span>
        <s class="price-list">$4,999</s>
        <span class="price-term">a year</span>
      </td>
      <td>
        <a class="btn btn-outline" href="https://www.linkedin.com/in/lucabriguglia">Get in touch</a>
      </td>
    </tr>
  </tbody>
</table>

Prices are in USD and exclude VAT or sales tax, which is added at checkout according to where you are. Paid monthly, a term costs a tenth of the yearly price, so a year paid monthly costs a fifth more than a year paid up front.

**Launch offer — half price, for as long as you keep the subscription.** The prices above are the offer prices: any paid edition bought on or before 31 December 2026 is half price for its first term, and renews at that same half price for every consecutive term the subscription runs unbroken. Let it lapse and take it up again later, and it is charged in full.
{: .note }

Enterprise is sold through a conversation rather than a checkout, because buyers at that size usually want a purchase order, a wire transfer and a signed copy of the agreement. Ask and you get all three.

## What happens after you buy

Checkout is handled by [Paddle](https://www.paddle.com), which is the merchant of record for the sale. Paddle takes the payment, applies the right VAT or sales tax for your location, accepts a company VAT or tax ID, and issues the tax invoice your finance team needs.

You then receive, by email, a licence certificate naming the licensee, the edition, the service count it covers and the term dates. Keep it for your own records and audits.

**There is no licence key, no activation and nothing to unlock.** Memoria Web is the same build whichever edition you run it under. Buying one changes what you are permitted to do, not what you install — so nothing in your deployment needs to know about it. Your Settings page lists the services each installed archive declares, which is how you check where you stand.
{: .note }

## Questions

**Does any of this affect the framework?** No. The packages are Apache 2.0 and always will be. You can build and ship anything with them without a licence from this page, and nothing here expires, meters or reports.

**How do I count services?** Count the entries in the `services` lists of the `memoria.json` manifests you have installed — one per named set of domain assemblies read over one connection string. Your Settings page lists them. Two instances reading the same service count it once, so a staging copy of production is not a second service.

**Does one licence cover several instances or environments?** Yes. An edition meters services, not instances, people or environments. Run as many copies as you like.

**Standard or Enterprise?** Standard and Professional each cover one legal entity. If you want a single licence to cover a parent and its subsidiaries, that is Enterprise, whatever the service count.

**Can I switch between monthly and yearly?** At a renewal, in either direction.

**What if we outgrow our edition?** License the edition that covers you. Get in touch and the change is made mid-term.

**Can I get an invoice, or pay by bank transfer?** Paddle issues a tax invoice for every purchase automatically. For a purchase order or a bank transfer, get in touch.

**Do you offer refunds?** Fees are paid in advance for the term and are non-refundable except where the law says otherwise. A month is the cheapest way to try a paid edition — and a single service costs nothing, indefinitely, if you want to see the tool working against a real store first.

**How do I cancel?** Give notice before the term ends and the subscription will not renew. After it you drop to the Community edition and its one-service limit; nothing about the framework, your stores or your data changes.

**I have another arrangement in mind.** Source escrow, a perpetual licence for one version, something the editions do not cover — [ask](https://www.linkedin.com/in/lucabriguglia).

<style>
  .term-toggle { display: flex; gap: 0.5rem; margin: 0 0 1.5rem; }
  .term-option[aria-pressed="true"] { box-shadow: inset 0 0 0 2px currentColor; }
  .pricing-table .price { white-space: nowrap; }
  .price-launch { font-weight: 600; }
  .price-list { margin-left: 0.25rem; opacity: 0.6; }
  .price-term { display: block; font-size: 0.75em; opacity: 0.7; }
</style>

<script src="https://cdn.paddle.com/paddle/v2/paddle.js"></script>
<script>
  // Paddle Billing configuration. What to create in Paddle, and where each of
  // these four values comes from, is in plans/paddle-setup.md.
  // Wired to the SANDBOX Memoria Web catalogue: three products metered by service, six prices at
  // $999 / $2,499 / $4,999 a year and a tenth monthly, and a recurring half-price discount that
  // expires 31/12/2026. The old framework products, priced by developer count, are archived in
  // sandbox; in the live account they are still there and still active, along with their six
  // prices and the old discount. Going live means archiving those, recreating this catalogue in
  // the live account, and swapping the four price IDs, the discount ID, the token and the
  // environment below — nothing else on the page changes.
  var MEMORIA_PADDLE = {
    environment: "sandbox",                        // "production" once the live Web catalogue exists
    token: "test_de21edefecb951c5cd1fd6eb729",     // Paddle > Developer tools > Authentication
    discountId: "dsc_01m31zknpmymywpsnrkjbmk68y",  // the recurring half-price launch discount
    discountCode: "",                              // or its code, which needs enabled_for_checkout on the discount
    successUrl: "https://lucabriguglia.github.io/Memoria/thank-you.html"
  };

  (function () {
    var config = MEMORIA_PADDLE;
    var term = "year";

    if (config.environment !== "production") {
      Paddle.Environment.set("sandbox");
    }
    Paddle.Initialize({ token: config.token });

    function showTerm(next) {
      term = next;

      document.querySelectorAll(".term-option").forEach(function (option) {
        option.setAttribute("aria-pressed", String(option.getAttribute("data-term") === term));
      });

      document.querySelectorAll(".price[data-launch-year]").forEach(function (cell) {
        cell.querySelector(".price-launch").textContent = cell.getAttribute("data-launch-" + term);
        cell.querySelector(".price-list").textContent = cell.getAttribute("data-list-" + term);
        cell.querySelector(".price-term").textContent = cell.getAttribute("data-term-" + term);
      });
    }

    document.querySelectorAll(".term-option").forEach(function (option) {
      option.addEventListener("click", function () {
        showTerm(option.getAttribute("data-term"));
      });
    });

    document.querySelectorAll(".buy").forEach(function (button) {
      button.addEventListener("click", function () {
        var edition = button.getAttribute("data-edition");
        var priceId = button.getAttribute("data-price-" + term);

        if (!priceId || priceId.indexOf("REPLACE") !== -1) {
          console.warn("[Memoria] No Paddle price ID configured for " + edition + " / " + term + ".");
          return;
        }

        var options = {
          items: [{ priceId: priceId, quantity: 1 }],
          customData: { edition: edition, term: term },
          settings: { displayMode: "overlay", theme: "light" }
        };
        if (config.discountId) {
          options.discountId = config.discountId;
        } else if (config.discountCode) {
          options.discountCode = config.discountCode;
        }
        if (config.successUrl) {
          options.settings.successUrl = config.successUrl;
        }

        Paddle.Checkout.open(options);
      });
    });
  })();
</script>
