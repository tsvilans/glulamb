# Constraints (prototype)

`GluLamb/Constraints`. Grasshopper: Rule Set and Rule Query, under "4. Blank". Written 2026-11-13.

A rule set answers questions of the form "what is the limit on this quantity, for these facts?".
It holds limits from a standard, a factory or a machine as data, not as code.

## Parts

- **Context**: the facts. Text (`species=southern-pine`, `step=press`) and numbers
  (`thickness=19`).
- **Rule**: a limit (`Max` or `Min`) on one quantity, with conditions. A condition tests one
  fact: text is equal, or a number is in a range. A rule has an id and a source (clause or
  reason).
- **RuleSet**: a named list of rules, with an optional parent.

## Queries

- `Find(quantity, context)` gives the matching rule with the most conditions. Of equals, the
  later rule wins. A set with no matching rule asks its parent. The answer holds the rule, so a
  report can name it.
- `Check(quantity, context, actual)` tests a value: passed, and how much of the limit it uses.
- `LargestWithin(ratioQuantity, key, amount, context)` solves for a number that its own rule
  depends on, e.g. the thickest lamella for a radius when the radius ratio changes with thickness.

## Own rules over a standard

Load your rules from JSON and set the standard as their parent. Your rules answer first. The
standard answers where yours are silent.

```json
{
  "Name": "Factory",
  "Rules": [
    { "Id": "press-capacity", "Quantity": "lamella.max-thickness", "Value": 30, "Kind": "Max",
      "Source": "press 2 takes 30 mm at most", "When": [ { "Key": "step", "Is": "press" } ] },
    { "Id": "hardwood", "Quantity": "lamella.radius-ratio", "Value": 250, "Kind": "Min",
      "Source": "own tests", "When": [ { "Key": "species-group", "Is": "hardwood" } ] }
  ]
}
```

## Lamination rules

`LaminationRules` has the rule sets `None`, `Eurocode` and `Ansi`, and the questions that
lamination asks: `ThickestLamella(radius)`, `RadiusRatio(thickness)`, `AllowedStrain(thickness)`
(1 / (2 x ratio)) and `AllowedTwistStrain(thickness)`.

| Quantity | Meaning | Kind |
| --- | --- | --- |
| `lamella.radius-ratio` | smallest bending radius over thickness | Min |
| `lamella.max-thickness` | largest thickness, mm | Max |
| `lamella.twist-strain` | largest strain from twist; without a rule, the bending strain | Max |

Context keys: `thickness` (mm), `species`, `species-group`, `step` (`bend`, `press`).

The values are the ones the `Standards` classes use. They are not checked again against the
standards. The ANSI set differs from `Standards.ANSI` in one place: a radius between 2.4 m and
2.8 m gives a lamella just under 19 mm, not 20 mm.

`TwoStepLamination` takes its thicknesses and strain limits from `TwoStepOptions.Rules` and
`Material`, and lists the rules that decided them.

## Not yet

- `GlulamData.Compute` and `Glulam.RadiusMultiplier` still use the `Standards` classes. Move them
  to rule sets, then remove the classes.
- Other quantities: lamella lengths, press and machine envelopes, blank sizes.
- Numbers only compare to fixed ranges. A limit that is a formula of other facts needs a new
  kind of rule.
