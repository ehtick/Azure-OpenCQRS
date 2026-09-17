---
title: Getting started
nav_order: 2
has_children: true
---

# Getting started

Memoria is two things behind one dispatcher. Used on its own it is a mediator: commands, queries and
notifications, each with a handler, each returning a `Result` rather than throwing. Add
`Memoria.EventSourcing` and a store and the same dispatcher sits in front of aggregates, streams and
projections.

Start with [Install](install.md), then take whichever quickstart matches what you came for. They are
independent — the mediator quickstart needs no store, and the event sourcing one assumes you have
read it.
