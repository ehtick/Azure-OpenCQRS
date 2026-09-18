---
title: Pricing
description: "What a Memoria commercial licence costs, who pays nothing, and how to buy a Standard, Professional or Enterprise edition."
nav_order: 10
---

# Pricing
{: .no_toc }

Memoria is dual-licensed. Most people owe nothing: the [Reciprocal Public License 1.5](https://opensource.org/license/rpl-1-5) is free, and so is the Community edition of the commercial licence. You pay only to keep your source closed while earning above the Community thresholds.

This page is the shop. The [Licence](license.html) page is the contract — the full terms of both licences, and the fees as published there form part of the agreement for the edition you accept.

1. TOC
{:toc}

## What you pay nothing for

You need no paid edition, and nothing to sign, if either of these describes you.

**You release your source under the RPL.** Use, modify and deploy Memoria free of charge, on the condition that the software you build with it is released under the RPL too. The condition is triggered by *deploying* for others, not only by shipping, so an internal line-of-business application counts. See [the RPL in short](license.html#the-reciprocal-public-license-in-short).

**You qualify for the Community edition.** Closed source, no fee, any number of developers, covering you and all your affiliates — for as long as you meet all three tests:

- under $5,000,000 USD annual gross revenue, or under $5,000,000 USD annual total budget for a registered non-profit, measured across you and your affiliates over the last twelve months;
- not a government or quasi-government agency; and
- never having taken more than $10,000,000 USD in aggregate outside capital, such as private equity or venture capital.

You assess your own eligibility — there is nothing to apply for and no key to collect. Add the package and build. If you stop qualifying you keep the Community licence for 90 days, which is the time you have to license a paid edition or move to the RPL. The full wording is in [Community edition eligibility](license.html#community-edition-eligibility).

## Paid editions

Every edition grants the same rights to the software: use Memoria in closed-source software, modify it, and ship it inside your products, across any number of projects. The editions differ only in who and how many people they cover, and in the support that comes with them.

<div class="term-toggle" role="group" aria-label="Billing term">
  <button type="button" class="btn term-option" data-term="year" aria-pressed="true">Yearly</button>
  <button type="button" class="btn term-option" data-term="month" aria-pressed="false">Monthly</button>
</div>

<table class="pricing-table">
  <thead>
    <tr>
      <th scope="col">Edition</th>
      <th scope="col">Scope</th>
      <th scope="col">Support</th>
      <th scope="col">Price</th>
      <th scope="col"></th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <th scope="row">Community</th>
      <td>You and all your affiliates, any number of developers, while you meet the <a href="license.html#community-edition-eligibility">eligibility terms</a></td>
      <td>GitHub issues</td>
      <td class="price">
        <span class="price-launch">Free</span>
        <span class="price-term">and always will be</span>
      </td>
      <td><a class="btn" href="getting-started/install.html">Start building</a></td>
    </tr>
    <tr>
      <th scope="row">Standard</th>
      <td>One legal entity, up to 10 developers</td>
      <td>GitHub issues</td>
      <td class="price"
          data-launch-year="$149.50" data-list-year="$299" data-term-year="a year"
          data-launch-month="$14.95" data-list-month="$29.90" data-term-month="a month">
        <span class="price-launch">$149.50</span>
        <s class="price-list">$299</s>
        <span class="price-term">a year</span>
      </td>
      <td>
        <button type="button" class="btn btn-primary buy"
                data-edition="standard"
                data-price-year="pri_01m2sk6t8ghk7cwyepy37jzcpq"
                data-price-month="pri_01m2sk6tcffhjnn0hbkszs1pp9">Buy Standard</button>
      </td>
    </tr>
    <tr>
      <th scope="row">Professional</th>
      <td>One legal entity, up to 50 developers</td>
      <td>Email, first response within two business days</td>
      <td class="price"
          data-launch-year="$499.50" data-list-year="$999" data-term-year="a year"
          data-launch-month="$49.95" data-list-month="$99.90" data-term-month="a month">
        <span class="price-launch">$499.50</span>
        <s class="price-list">$999</s>
        <span class="price-term">a year</span>
      </td>
      <td>
        <button type="button" class="btn btn-primary buy"
                data-edition="professional"
                data-price-year="pri_01m2sk6tp37fyfvbarj24e79vj"
                data-price-month="pri_01m2sk6tt29w4qkg8wz5k2fhg9">Buy Professional</button>
      </td>
    </tr>
    <tr>
      <th scope="row">Enterprise</th>
      <td>You and all your affiliates, unlimited developers</td>
      <td>Email, first response within one business day</td>
      <td class="price"
          data-launch-year="$1,499.50" data-list-year="$2,999" data-term-year="a year"
          data-launch-month="$149.95" data-list-month="$299.90" data-term-month="a month">
        <span class="price-launch">$1,499.50</span>
        <s class="price-list">$2,999</s>
        <span class="price-term">a year</span>
      </td>
      <td>
        <a class="btn btn-outline" href="https://www.linkedin.com/in/lucabriguglia">Get in touch</a>
      </td>
    </tr>
  </tbody>
</table>

Prices are in USD and exclude VAT or sales tax, which is added at checkout according to where you are. Paid monthly, a term costs a tenth of the yearly price, so a year paid monthly costs a fifth more than a year paid up front.

**Launch offer — half price, for as long as you keep the subscription.** The prices above are the beta prices: any paid edition bought before the stable 2.0.0 release is half price for its first term, and renews at that same half price for every consecutive term the subscription runs unbroken. Let it lapse and take it up again later, and it is charged in full. Early adopters take a beta on; the offer is what that is worth.
{: .note }

Enterprise is sold through a conversation rather than a checkout, because buyers at that size usually want a purchase order, a wire transfer and a signed copy of the agreement. Ask and you get all three.

## What happens after you buy

Checkout is handled by [Paddle](https://www.paddle.com), which is the merchant of record for the sale. Paddle takes the payment, applies the right VAT or sales tax for your location, accepts a company VAT or tax ID, and issues the tax invoice your finance team needs.

You then receive, by email, a licence certificate naming the licensee, the edition, the developer count it covers and the term dates. Keep it for your own records and audits.

**There is no licence key, no activation and nothing to unlock.** Memoria is the same package on NuGet whichever licence you use it under. Buying an edition changes what you are permitted to do, not what you install — so nothing in your build or deployment needs to know about it.
{: .note }

## Questions

**How do I count developers?** A developer is any individual, employee or contractor, who writes, modifies, compiles or builds source code that references Memoria at any time during the licence term. Someone who only runs the finished product is not a developer.

**Does one licence cover several projects?** Yes. A paid edition covers any number of projects and products for the entities in its scope.

**Standard or Enterprise?** Standard and Professional each cover one legal entity. If you want a single licence to cover a parent and its subsidiaries, that is Enterprise, whatever the headcount.

**Can I switch between monthly and yearly?** At a renewal, in either direction.

**What if we outgrow our edition?** License the edition that covers you. Get in touch and the change is made mid-term.

**Can I get an invoice, or pay by bank transfer?** Paddle issues a tax invoice for every purchase automatically. For a purchase order or a bank transfer, get in touch.

**Do you offer refunds?** Fees are paid in advance for the term and are non-refundable except where the law says otherwise. A month is the cheapest way to try a paid edition — or evaluate under the RPL first, which costs nothing.

**How do I cancel?** Give notice before the term ends and the subscription will not renew. After it, either move to the RPL or stop building against Memoria; products you have already shipped or deployed keep running.

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
  var MEMORIA_PADDLE = {
    environment: "sandbox",                        // "production" once the live prices exist
    token: "test_de21edefecb951c5cd1fd6eb729",     // Paddle > Developer tools > Authentication
    discountId: "dsc_01m2skdarjg2e39m16smdw5fs0",  // the recurring half-price beta discount
    discountCode: "",                              // or its code, which needs enabled_for_checkout on the discount
    successUrl: ""                                 // e.g. "https://lucabriguglia.github.io/Memoria/thank-you.html"
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
