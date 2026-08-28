# ADR-0001: Prices are computed in C#, never by the language model

- **Date:** 2026-08-26
- **Status:** accepted

## Context

QuoteDesk sends priced quotations to real customers. A quotation is a commercial commitment: once it
leaves, the price on it is the price. The system's inputs are unstructured — a human's hurried email —
so a language model is genuinely the right tool for reading them. The question is how far that model's
authority extends.

Language models produce plausible arithmetic. Plausible is fine for a summary and unacceptable for a
figure a customer will hold us to. A discount that is 8% instead of 6% is not a visible failure: it
looks entirely normal, passes review, and is discovered a quarter later in the margin report.

## Options considered

**Let the model compute prices from rules in its prompt.** Genuinely appealing: far less code, and it
handles novel cases — an unusual bundle, an unlisted combination — without anyone writing a rule for
them. Much of the pricing logic disappears into the prompt.

**Let the model compute, then validate the result in code.** A middle path with real merit: the model
keeps its flexibility, and a checker catches anything outside tolerance. Cheaper than reimplementing
every rule.

**Compute everything in code; the model only supplies quantities and SKUs and explains the outcome.**
The most code, and the least flexible when a case falls outside the rules.

## Decision

Every number is computed in `QuoteDesk.Domain`: list price, slab discount, tier discount, margin
floor, freight, tax, delivery date. That project has zero dependencies, cannot reach the network, and
takes time as a parameter. The model reads the enquiry, decides what the customer means, chooses which
tools to call, and puts the result into a sentence. It never performs arithmetic.

Validation-after-the-fact was rejected because writing the validator requires implementing the rules
anyway — at which point the model's computation is redundant work with a failure mode attached.

## Consequences

Pricing cases outside the encoded rules cannot be quoted automatically. They surface as
`requires_override` and go to a human. That is the intended behaviour, but it does mean the rule
tables must be maintained, and an unmaintained table quietly degrades into an approval queue nobody
can clear.

The domain project must be tested to a standard the rest of the codebase is not held to, including
every boundary condition, because nothing downstream will catch an error in it.

We give up the model's ability to handle a genuinely novel commercial situation. In exchange, no
quotation ever leaves with an invented number in it. For a document a customer can hold us to, that
is the correct trade.

The signal to revisit: if the override queue becomes the normal path rather than the exception, the
rules are too narrow — and the fix is better rules, not a more trusted model.
