---
title: Tools
description: "Memoria Web, the browser tool for reading an event-sourced store, is a separate product developed in its own repository. What it is, where it is going, and how it relates to the framework."
nav_order: 6
redirect_from:
  - /tools/memoria-web.html
  - /tools/memoria-web-configuration.html
  - /tools/memoria-web-deployment.html
  - /tools/memoria-web-samples.html
---

# Tools

## Memoria Web

Memoria Web is a browser tool for reading an event-sourced store: the events appended to it, the
aggregates and projections snapshotted from them, and the types both were written through. Point it
at a database, upload a zip of your own domain assemblies with a `memoria.json` at its root, and it
folds an aggregate from its events so you can see the state a snapshot stands at and how far behind
its history it is. It creates nothing and deletes nothing.

It began in this repository as a reader of Memoria stores. It now lives in its own repository, which
is private, and its documentation lives with it: the pages that used to sit here — what each page
shows, configuration, deployment and the sample data — moved there with the code, and the addresses
they had redirect to this one.

**It is being made to read any framework's store, not only Memoria's.** A service tells the tool,
through its manifest, which of the uploaded types are the streams, events, aggregates and
projections, how they are named and folded, and where in the store the events and snapshots are.
Memoria's own stores are described the same way as anyone else's; the tool knows no framework by
name.

It is a commercial product, separate from the framework: free over a single service, and paid above
that, under the [Memoria Web Licence](../license.md#memoria-web-licence-agreement). The framework
packages are Apache 2.0 regardless, nothing in them depends on the tool, and nothing in the tool
changes their licence.

There is a hosted instance at [demo.getmemoria.io](https://demo.getmemoria.io), behind its sign-in,
so access is by invitation: message me on [LinkedIn](https://www.linkedin.com/in/lucabriguglia) and
I will send you one. Ask the same way about anything else to do with the tool.

## Related

- [Licence](../license.md) — Apache 2.0 for the framework, and the terms the tool is run under
- [Release notes](../release-notes.md) — the versions of the framework the tool was developed alongside
