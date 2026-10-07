# GluLamb.Gallery

Lays out every registered joint type over a grid of joint conditions, checks each one, and writes
the joint catalogue. It is both the joint test suite and the way to see what the library holds.

For each joint type, the gallery takes every condition the registry offers it for: a free end, a
point on a face, splices, corners, forks, T joints, crossings, K joints (with and without a joist), a
post corner, a post running past two beams, and four beam ends. It builds each of those as drawn and
with the beam sections turned a quarter turn, each with and without Flip. A cell passes when:

- the joint builds (status Ok or Partial),
- no two of the cut beams overlap (by more than 1 cm³), and
- every beam keeps at least 30% of its volume (it wasn't cut in the wrong place).

## Running

Needs Rhino 8 installed (it runs Rhino headless through Rhino.Inside). Build and run from the repo:

```
dotnet run --project GluLamb.Gallery -- --docs docs/joints
```

Options:

- `--out folder`: results (default `gallery-out`): `summary.txt`, and per joint type in `types/` a
  `.json` of its cells, a `.3dm` of its grid and a `.png` drawing.
- `--docs folder`: write the catalogue there (`README.md` and an SVG per type).
- `--types text`: only joint types whose id contains the text.
- `--no-3dm`: skip the `.3dm` files.
- `--verbose`: list each beam's features and how much of the beam is kept.

The exit code is 1 if any cell fails. Each joint type runs in its own process, because Rhino can
lose its licence partway through a long run (for example to another Rhino session); a type whose
process loses it is tried again.

The project builds GluLamb into its own `obj/GluLamb` folder, not the shared `bin` plug-in folder.

## Adding conditions

Conditions are in `Conditions.cs`: each builds some beams and the joint condition between them, for
a given quarter-turn rotation of each beam's section. New joint types appear in the gallery and the
catalogue automatically, wherever the registry offers them.
