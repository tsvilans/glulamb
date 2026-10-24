using System.Diagnostics;
using System.Runtime.CompilerServices;
using GluLamb;
using GluLamb.Blanks;
using GluLamb.Joints;
using Rhino.Geometry;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        RhinoInside.Resolver.Initialize();
        return Run();
    }

    static int failures = 0;
    const double Tol = 5.0;

    // A length tolerance that lets any shorter beam be cut from any longer blank
    const double Unbounded = 1e9;

    static double LengthTolerance(bool partial) => partial ? Unbounded : 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Run()
    {
        using (new Rhino.Runtime.InProcess.RhinoCore(new[] { "/netcore", "/nosplash" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow))
        {
            TestCurved(false);
            TestCurved(true);
            TestStraight(false);
            TestStraight(true);
            TestMirror();
            TestLengthBands(false);
            TestLengthBands(true);
            TestLengthChain();
            TestSections();
            TestOffsetGlulam();
            TestFitSection();
            TestTiming(1000, 0);
            TestTiming(1000, 500);
            TestTiming(1000, Unbounded);
        }
        Console.WriteLine(failures == 0 ? "ALL PASSED" : $"{failures} FAILURES");
        return failures == 0 ? 0 : 1;
    }

    static void Check(bool ok, string what)
    {
        if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); }
    }

    // Beam with a fixed Y vector (physical, so it survives transforms and reversal)
    static Beam MakeBeam(Curve c, double w, double h, Vector3d y) =>
        new Beam { Centreline = c, Width = w, Height = h, Orientation = new VectorOrientation(y) };

    static Transform RandomRigid(Random rng)
    {
        var axis = new Vector3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5);
        axis.Unitize();
        var rot = Transform.Rotation(rng.NextDouble() * Math.PI * 2, axis, Point3d.Origin);
        var move = Transform.Translation(new Vector3d(rng.NextDouble(), rng.NextDouble(), rng.NextDouble()) * 50000);
        return move * rot;
    }

    static Beam Copy(Beam b, Vector3d y, Random rng, double perturb, bool reverse, bool roll)
    {
        var c = b.Centreline.ToNurbsCurve();
        if (perturb > 0)
            for (int i = 0; i < c.Points.Count; ++i)
            {
                var v = new Vector3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5);
                v.Unitize();
                var cp = c.Points[i];
                c.Points.SetPoint(i, cp.Location + v * perturb * rng.NextDouble(), cp.Weight);
            }
        if (reverse) c.Reverse();
        var copy = MakeBeam(c, b.Width, b.Height, roll ? -y : y);
        copy.Transform(RandomRigid(rng));
        return copy;
    }

    /// <summary>
    /// The blank, moved by each member's transform, must lie on the member: centreline within
    /// tolerance along the member (and its extensions), and section Y within tolerance at the section edge.
    /// </summary>
    static double Verify(IList<Beam> beams, BeamClusterResult r, IList<BeamExtension> ext = null)
    {
        double worst = 0;
        for (int i = 0; i < beams.Count; ++i)
        {
            var blank = r.PlacedBlanks[i];
            Check(blank.Id == r.Clusters[r.BlankIndex[i]].Blank.Id, "placed blank lost its blank's id");
            var m = beams[i];
            var curve = m.Centreline;
            if (ext != null)
            {
                if (ext[i].Start > 0) curve = curve.Extend(CurveEnd.Start, ext[i].Start, CurveExtensionStyle.Line);
                if (ext[i].End > 0) curve = curve.Extend(CurveEnd.End, ext[i].End, CurveExtensionStyle.Line);
            }
            foreach (var t in curve.DivideByCount(60, true))
            {
                var p = curve.PointAt(t);
                blank.Centreline.ClosestPoint(p, out double tb);
                var q = blank.Centreline.PointAt(tb);
                double d = p.DistanceTo(q);
                // Off the blank's ends counts as missing coverage
                if ((tb <= blank.Centreline.Domain.Min + 1e-9 || tb >= blank.Centreline.Domain.Max - 1e-9) && d > 1e-3) d = Math.Max(d, 1e6);

                double roll = 0;
                if (curve == m.Centreline)
                {
                    var ym = m.GetPlane(t).YAxis;
                    var yb = blank.GetPlane(tb).YAxis;
                    roll = 0.5 * Math.Max(m.Width, m.Height) * Math.Min((ym - yb).Length, (ym + yb).Length);
                }
                worst = Math.Max(worst, Math.Max(d, roll));
            }
        }
        return worst;
    }

    static string Sizes(BeamClusterResult r) => string.Join(",", r.Clusters.Select(c => c.Members.Count));

    static void Report(string name, IList<Beam> beams, BeamClusterResult r, long ms, IList<BeamExtension> ext = null)
    {
        double worst = Verify(beams, r, ext);
        Console.WriteLine($"{name}: {beams.Count} beams -> {r.Clusters.Count} blanks [{Sizes(r)}], partial {r.Partial.Count(x => x)}, " +
            $"max dev {r.Deviation.Max():0.00}, blank fit {worst:0.00}, {r.Alignments} alignments, {ms} ms");
        // Pairwise within tol means members within tol of the reference; the fit check adds
        // interpolation/sampling slack, so allow a little over.
        Check(r.Deviation.Max() <= Tol, $"{name}: deviation over tolerance");
        Check(worst <= Tol * 1.2, $"{name}: transformed blank misses a member by {worst:0.00}");
    }

    static void TestCurved(bool partial)
    {
        var rng = new Random(1);
        var up = Vector3d.ZAxis;
        var bases = new List<(Beam Beam, Vector3d Y)>();
        void Add(Curve c, Vector3d y) => bases.Add((MakeBeam(c, 100, 400, y), y));

        Add(new Arc(new Circle(Plane.WorldXY, 3000), new Interval(0, Math.PI / 3)).ToNurbsCurve(), up);
        Add(new Arc(new Circle(Plane.WorldXY, 5000), new Interval(0, 0.7)).ToNurbsCurve(), up);
        Add(new Arc(new Circle(Plane.WorldXY, 3000), new Interval(0, 0.7)).ToNurbsCurve(), up);  // sub-arc of the first
        Add(Curve.CreateInterpolatedCurve(new[] { new Point3d(0, 0, 0), new Point3d(2000, 800, 300), new Point3d(4000, 500, 900), new Point3d(6000, -300, 1200) }, 3), up);
        Add(Curve.CreateInterpolatedCurve(new[] { new Point3d(0, 0, 0), new Point3d(1500, 1200, -200), new Point3d(3500, 1500, 400), new Point3d(5000, 600, 1500) }, 3), up);
        var helix = Curve.CreateInterpolatedCurve(Enumerable.Range(0, 13).Select(i => new Point3d(4000 * Math.Cos(i * 0.2), 4000 * Math.Sin(i * 0.2), i * 300.0)), 3);
        Add(helix, Vector3d.XAxis);

        // The first arc with its section rolled 10 degrees: a different beam
        var tilted = up; tilted.Rotate(10 * Math.PI / 180, Vector3d.XAxis);
        Add(new Arc(new Circle(Plane.WorldXY, 3000), new Interval(0, Math.PI / 3)).ToNurbsCurve(), tilted);

        var beams = new List<Beam>();
        var expect = new List<int>();
        for (int k = 0; k < bases.Count; ++k)
        {
            beams.Add(bases[k].Beam); expect.Add(k);
            for (int c = 0; c < 8; ++c)
            {
                beams.Add(Copy(bases[k].Beam, bases[k].Y, rng, 0.5, rng.Next(2) == 1, rng.Next(2) == 1));
                expect.Add(k);
            }
        }

        // Some extensions the blank must cover
        var ext = beams.Select((b, i) => i % 5 == 0 ? new BeamExtension(100, 30) : new BeamExtension()).ToList();

        var sw = Stopwatch.StartNew();
        var r = BeamClustering.Cluster(beams, Tol, LengthTolerance(partial), 24, ext);
        sw.Stop();
        Report($"Curved (partial {partial})", beams, r, sw.ElapsedMilliseconds, ext);

        // Same family -> same blank; with partial, the sub-arc family joins the first
        int Family(int k) => partial && k == 2 ? 0 : k;
        var blankOf = new Dictionary<int, int>();
        for (int i = 0; i < beams.Count; ++i)
        {
            int f = Family(expect[i]);
            if (!blankOf.ContainsKey(f)) blankOf[f] = r.BlankIndex[i];
            Check(blankOf[f] == r.BlankIndex[i], $"curved: beam {i} (family {expect[i]}) not with its family");
        }
        Check(blankOf.Values.Distinct().Count() == blankOf.Count, "curved: two families share a blank");
        Check(r.Clusters.Count == (partial ? 6 : 7), $"curved: expected {(partial ? 6 : 7)} blanks, got {r.Clusters.Count}");
        if (partial)
            Check(Enumerable.Range(0, beams.Count).Where(i => expect[i] == 2).All(i => r.Partial[i]), "curved: sub-arcs should be partial");
    }

    static void TestStraight(bool partial)
    {
        var rng = new Random(2);
        var beams = new List<Beam>();
        var labels = new List<string>();
        void Add(string label, double length, double w, double h, double rollDeg, int count)
        {
            var y = Vector3d.ZAxis;
            y.Rotate(rollDeg * Math.PI / 180, Vector3d.XAxis);
            var b = MakeBeam(new LineCurve(Point3d.Origin, new Point3d(length, 0, 0)), w, h, y);
            for (int i = 0; i < count; ++i)
            {
                beams.Add(Copy(b, y, rng, 0.3, rng.Next(2) == 1, rng.Next(2) == 1));
                labels.Add(label);
            }
        }

        Add("3000", 3000, 100, 200, 0, 5);
        Add("3002", 3002, 100, 200, 0, 3);      // within tolerance of 3000
        Add("3100", 3100, 100, 200, 0, 3);
        Add("2000", 2000, 100, 200, 0, 3);
        Add("sec240", 3000, 100, 240, 0, 3);    // different section
        Add("sec103", 3000, 103, 200, 0, 2);    // section within tolerance
        // Rolled about its own axis, a straight beam is the same beam
        Add("roll10", 3000, 100, 200, 10, 2);

        var sw = Stopwatch.StartNew();
        var r = BeamClustering.Cluster(beams, Tol, LengthTolerance(partial), 24);
        sw.Stop();
        Report($"Straight (partial {partial})", beams, r, sw.ElapsedMilliseconds);

        int Blank(string l) => r.BlankIndex[labels.IndexOf(l)];
        bool Together(string a, string b) => Enumerable.Range(0, beams.Count).Where(i => labels[i] == a || labels[i] == b).Select(i => r.BlankIndex[i]).Distinct().Count() == 1;

        Check(Together("3000", "3002"), "straight: 3000 and 3002 should share");
        Check(Together("3000", "sec103"), "straight: section within tolerance should share");
        Check(Blank("sec240") != Blank("3000"), "straight: different section shared");
        Check(Together("roll10", "3000"), "straight: a straight beam rolled about its axis should share");
        if (!partial)
        {
            Check(Blank("3100") != Blank("3000") && Blank("2000") != Blank("3000") && Blank("2000") != Blank("3100"), "straight: different lengths shared without partial");
        }
        else
        {
            Check(Together("3100", "3000") && Together("3100", "2000"), "straight: partial should put 2000/3000 into the 3100 blank");
            Check(r.Clusters[Blank("3100")].Blank.Centreline.GetLength() >= 3100 - 1e-6, "straight: blank shorter than the longest member");
        }
        var b0 = r.Clusters[Blank("3000")].Blank;
        Console.WriteLine($"  blank of 3000 group: {b0.Width:0.0} x {b0.Height:0.0}, length {b0.Centreline.GetLength():0.0}");
        Check(b0.Width >= 103 - 1e-9, "straight: blank narrower than the widest member");
    }

    static void TestMirror()
    {
        var rng = new Random(3);
        var c = Curve.CreateInterpolatedCurve(new[] { new Point3d(0, 0, 0), new Point3d(2000, 900, 400), new Point3d(4000, 400, 1300), new Point3d(5500, -600, 1600), new Point3d(7000, -200, 2600) }, 3);
        var y = Vector3d.ZAxis;
        var a = MakeBeam(c, 120, 360, y);
        var mirrored = a.Duplicate();
        mirrored.Transform(Transform.Mirror(Plane.WorldZX));
        var my = y; my.Transform(Transform.Mirror(Plane.WorldZX));

        var beams = new List<Beam> { a, mirrored };
        for (int i = 0; i < 4; ++i) beams.Add(Copy(a, y, rng, 0.5, rng.Next(2) == 1, rng.Next(2) == 1));
        for (int i = 0; i < 4; ++i) beams.Add(Copy(mirrored, my, rng, 0.5, rng.Next(2) == 1, rng.Next(2) == 1));

        foreach (bool partial in new[] { false, true })
        {
            var sw = Stopwatch.StartNew();
            var r = BeamClustering.Cluster(beams, Tol, LengthTolerance(partial), 24);
            sw.Stop();
            Report($"Mirror (partial {partial})", beams, r, sw.ElapsedMilliseconds);
            Check(r.Clusters.Count == 2, $"mirror: expected 2 blanks, got {r.Clusters.Count}");
            Check(r.BlankIndex[0] != r.BlankIndex[1], "mirror: mirrored copy clustered with the original");
        }
    }

    /// <summary>
    /// 3 beams around 2000 +-200 and 7 around 3000 +-100, length tolerance 500: two blanks,
    /// 2200 and 3100 long. Straight, and as arcs of one radius.
    /// </summary>
    static void TestLengthBands(bool curved)
    {
        var rng = new Random(6);
        double[] lengths = { 1800, 2000, 2200, 2900, 2950, 3000, 3000, 3050, 3080, 3100 };
        var beams = new List<Beam>();
        foreach (var length in lengths)
        {
            Curve c = curved
                ? new Arc(new Circle(Plane.WorldXY, 4000), new Interval(0, length / 4000)).ToNurbsCurve()
                : new LineCurve(Point3d.Origin, new Point3d(length, 0, 0));
            beams.Add(Copy(MakeBeam(c, 100, 200, Vector3d.ZAxis), Vector3d.ZAxis, rng, 0.3, rng.Next(2) == 1, rng.Next(2) == 1));
        }

        var name = $"Length bands ({(curved ? "arcs" : "straight")}, length tolerance 500)";
        var sw = Stopwatch.StartNew();
        var r = BeamClustering.Cluster(beams, Tol, 500, 24);
        sw.Stop();
        Report(name, beams, r, sw.ElapsedMilliseconds);

        var blankLengths = r.Clusters.Select(x => x.Blank.Centreline.GetLength()).OrderBy(x => x).ToList();
        Console.WriteLine($"  blank lengths: {string.Join(", ", blankLengths.Select(x => x.ToString("0.0")))}");
        Check(r.Clusters.Count == 2, $"{name}: expected 2 blanks, got {r.Clusters.Count}");
        Check(blankLengths.Count == 2 && Math.Abs(blankLengths[0] - 2200) < 2 * Tol && Math.Abs(blankLengths[1] - 3100) < 2 * Tol, $"{name}: expected blanks of 2200 and 3100");
        Check(r.BlankIndex.Take(3).Distinct().Count() == 1 && r.BlankIndex.Skip(3).Distinct().Count() == 1, $"{name}: wrong bands");

        // Without a length tolerance, only near-equal lengths share
        var r0 = BeamClustering.Cluster(beams, Tol, 0, 24);
        Console.WriteLine($"  with length tolerance 0: {r0.Clusters.Count} blanks [{Sizes(r0)}]");
        Check(r0.Partial.All(x => !x), $"{name}: partial members with length tolerance 0");
    }

    /// <summary>
    /// Evenly spread lengths must split into bands no wider than the length tolerance, not chain.
    /// </summary>
    static void TestLengthChain()
    {
        var rng = new Random(7);
        var beams = new List<Beam>();
        foreach (var length in new double[] { 2000, 2300, 2600, 2900, 3200 })
            for (int i = 0; i < 2; ++i)
                beams.Add(Copy(MakeBeam(new LineCurve(Point3d.Origin, new Point3d(length, 0, 0)), 100, 200, Vector3d.ZAxis), Vector3d.ZAxis, rng, 0.3, rng.Next(2) == 1, false));

        var sw = Stopwatch.StartNew();
        var r = BeamClustering.Cluster(beams, Tol, 500, 24);
        sw.Stop();
        Report("Length chain (2000..3200 by 300, length tolerance 500)", beams, r, sw.ElapsedMilliseconds);
        foreach (var c in r.Clusters)
        {
            var ls = c.Members.Select(m => beams[m].Centreline.GetLength()).ToList();
            Console.WriteLine($"  blank {c.Blank.Centreline.GetLength():0}: members {string.Join(", ", ls.Select(x => x.ToString("0")))}");
            Check(ls.Max() - ls.Min() <= 500 + 1e-6, "length chain: a band is wider than the length tolerance");
        }
        Check(r.Clusters.Count == 3, $"length chain: expected 3 blanks, got {r.Clusters.Count}");
    }

    /// <summary>
    /// One curve with different sections: sections within tolerance share (the blank as big as
    /// the largest plus deviations), the others don't.
    /// </summary>
    static void TestSections()
    {
        var rng = new Random(8);
        var curve = Curve.CreateInterpolatedCurve(new[] { new Point3d(0, 0, 0), new Point3d(2000, 700, 300), new Point3d(4000, 400, 900), new Point3d(5500, -300, 1300) }, 3);
        var sections = new (double W, double H)[] { (100, 200), (102, 200), (100, 203), (140, 200), (100, 400), (200, 100) };
        var beams = new List<Beam>();
        var labels = new List<string>();
        foreach (var (w, h) in sections)
            for (int i = 0; i < 3; ++i)
            {
                beams.Add(Copy(MakeBeam(curve, w, h, Vector3d.ZAxis), Vector3d.ZAxis, rng, 0.5, rng.Next(2) == 1, rng.Next(2) == 1));
                labels.Add($"{w}x{h}");
            }

        var sw = Stopwatch.StartNew();
        var r = BeamClustering.Cluster(beams, Tol, 0, 24);
        sw.Stop();
        Report("Sections (one curve)", beams, r, sw.ElapsedMilliseconds);
        foreach (var c in r.Clusters)
            Console.WriteLine($"  blank {c.Blank.Width:0.0} x {c.Blank.Height:0.0}: {string.Join(", ", c.Members.Select(m => labels[m]).Distinct())}");

        int Blank(string l) => r.BlankIndex[labels.IndexOf(l)];
        Check(Blank("100x200") == Blank("102x200") && Blank("100x200") == Blank("100x203"), "sections: sections within tolerance should share");
        Check(new[] { "100x200", "140x200", "100x400", "200x100" }.Select(Blank).Distinct().Count() == 4, "sections: different sections share");
        var shared = r.Clusters[Blank("100x200")].Blank;
        Check(shared.Width >= 102 && shared.Height >= 203, "sections: blank smaller than a member");
        Check(r.Clusters.Count == 4, $"sections: expected 4 blanks, got {r.Clusters.Count}");
    }

    /// <summary>
    /// A glulam made from an offset beam goes along the beam's section centres, with no offsets,
    /// for a curved beam and for a straight beam whose section twists.
    /// </summary>
    static void TestOffsetGlulam()
    {
        var curve = Curve.CreateInterpolatedCurve(new[] { new Point3d(0, 0, 0), new Point3d(2000, 700, 300), new Point3d(4000, 400, 900), new Point3d(5500, -300, 1300) }, 3);
        var curved = new Beam { Centreline = curve, Width = 100, Height = 300, OffsetX = 30, OffsetY = -50, Orientation = new VectorOrientation(Vector3d.ZAxis) };

        var line = new LineCurve(Point3d.Origin, new Point3d(4000, 0, 0));
        var twist = new VectorListOrientation(line, new[] { 0.0, 4000.0 }, new[] { Vector3d.ZAxis, Vector3d.YAxis });
        var twisted = new Beam { Centreline = line, Width = 100, Height = 300, OffsetX = 0, OffsetY = 100, Orientation = twist };

        foreach (var (name, beam) in new[] { ("curved", curved), ("twisted", twisted) })
        {
            var glulam = Glulam.CreateGlulam(beam, beam.Orientation, GluLamb.Standards.Standard.Eurocode);
            Beam asBeam = glulam;
            double worst = 0, worstAngle = 0;
            foreach (var t in beam.Centreline.DivideByCount(50, true))
            {
                var plane = beam.GetPlane(t);
                var centre = plane.PointAt(beam.OffsetX, beam.OffsetY);
                glulam.Centreline.ClosestPoint(centre, out double tg);
                worst = Math.Max(worst, centre.DistanceTo(glulam.Centreline.PointAt(tg)));
                worstAngle = Math.Max(worstAngle, Vector3d.VectorAngle(plane.YAxis, glulam.GetPlane(tg).YAxis));
            }
            Console.WriteLine($"Offset glulam ({name}): {glulam.GetType().Name}, centreline off the section centres by {worst:0.000}, Y-axes by {Rhino.RhinoMath.ToDegrees(worstAngle):0.00} deg");
            Check(asBeam.OffsetX == 0 && asBeam.OffsetY == 0, $"offset glulam ({name}): has offsets");
            Check(worst < 0.1, $"offset glulam ({name}): centreline {worst:0.000} off the section centres");
            Check(worstAngle < Rhino.RhinoMath.ToRadians(0.5), $"offset glulam ({name}): orientation off by {Rhino.RhinoMath.ToDegrees(worstAngle):0.00} deg");

            bool threw = false;
            try { asBeam.OffsetX = 5; } catch (InvalidOperationException) { threw = true; }
            Check(threw, $"offset glulam ({name}): setting an offset didn't throw");
        }
    }

    /// <summary>
    /// FitSection rounds sections up to whole lamellae; Width/Height can't be set; the minimum
    /// lamella size; a curve too tight for the standard is flagged; Cluster Beams with a lamella size.
    /// </summary>
    static void TestFitSection()
    {
        var straight = new Beam { Centreline = new LineCurve(Point3d.Origin, new Point3d(3000, 0, 0)), Width = 100, Height = 205, Orientation = new VectorOrientation(Vector3d.ZAxis) };
        var glulam = Glulam.CreateGlulam(straight, straight.Orientation, GluLamb.Standards.Standard.Eurocode);
        glulam.Data.LamWidth = 120;
        glulam.Data.LamHeight = 40;
        var size = glulam.FitSection(100, 205);
        Console.WriteLine($"FitSection 100 x 205 with 120 x 40 lamellae: {size.Width} x {size.Height} ({glulam.Data.NumWidth} x {glulam.Data.NumHeight})");
        Check(size.Width == 120 && size.Height == 240 && glulam.Height == 240, "fit section: 100 x 205 should round up to 120 x 240");

        size = glulam.FitSection(120, 200 + 1e-8);
        Check(size.Height == 200, $"fit section: 200 (plus a hair) should stay 5 lamellae, got {size.Height}");

        bool threw = false;
        try { glulam.Width = 50; } catch (InvalidOperationException) { threw = true; }
        Check(threw, "fit section: setting Width didn't throw");

        threw = false;
        glulam.Data.LamHeight = 2;
        try { glulam.FitSection(120, 200); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "fit section: lamellae under the minimum weren't rejected");

        // Radius 500, 200 deep: the standard wants lamellae of about 2 mm, under the 5 mm minimum
        var tight = new Beam { Centreline = new Arc(new Circle(Plane.WorldXY, 500), new Interval(0, 1.5)).ToNurbsCurve(), Width = 100, Height = 200, Orientation = new VectorOrientation(Vector3d.ZAxis) };
        var tightGlulam = Glulam.CreateGlulam(tight, tight.Orientation, GluLamb.Standards.Standard.Eurocode);
        var warnings = tightGlulam.Data.Warnings();
        Console.WriteLine($"Tight curve: lamellae {tightGlulam.Data.LamWidth:0.##} x {tightGlulam.Data.LamHeight:0.##}, standard {tightGlulam.Data.MaxLamWidth:0.##} x {tightGlulam.Data.MaxLamHeight:0.##}: {string.Join(" ", warnings)}");
        Check(tightGlulam.Data.ExceedsStandard && warnings.Count > 0, "fit section: a curve too tight for the standard wasn't flagged");

        // Cluster Beams with 40 mm lamellae: blank heights in whole lamellae
        var rng = new Random(9);
        var beams = Enumerable.Range(0, 3).Select(i => Copy(MakeBeam(new Arc(new Circle(Plane.WorldXY, 6000), new Interval(0, 0.6)).ToNurbsCurve(), 100, 203, Vector3d.ZAxis), Vector3d.ZAxis, rng, 0.3, i == 1, false)).ToList();
        var r = BeamClustering.Cluster(beams, Tol, 0, 24, null, GluLamb.Standards.Standard.Eurocode, 0, 40);
        var blank = r.Clusters[0].Blank;
        Console.WriteLine($"Cluster with 40 mm lamellae: {r.Clusters.Count} blank, {blank.Width:0.##} x {blank.Height:0.##} ({blank.Data.NumWidth} x {blank.Data.NumHeight} lamellae of {blank.Data.LamWidth:0.##} x {blank.Data.LamHeight:0.##})");
        Check(r.Clusters.Count == 1 && blank.Height == 240 && blank.Width >= 100, "fit section: cluster blank not 240 deep in 40 mm lamellae");
    }

    static void TestTiming(int count, double lengthTolerance)
    {
        var rng = new Random(4);
        int families = count / 20;
        var beams = new List<Beam>();
        var family = new List<int>();
        var y = Vector3d.ZAxis;
        for (int f = 0; f < families; ++f)
        {
            // Random spline, 4-12 m, gentle curvature, a few sections
            double length = 4000 + rng.NextDouble() * 8000;
            var pts = Enumerable.Range(0, 5).Select(i => new Point3d(i * length / 4, (rng.NextDouble() - 0.5) * 1200, (rng.NextDouble() - 0.5) * 600)).ToArray();
            double h = new[] { 200.0, 280, 360 }[rng.Next(3)];
            var b = MakeBeam(Curve.CreateInterpolatedCurve(pts, 3), 100, h, y);
            for (int i = 0; i < 20; ++i)
            {
                beams.Add(i == 0 ? b : Copy(b, y, rng, 0.5, rng.Next(2) == 1, rng.Next(2) == 1));
                family.Add(f);
            }
        }

        var sw = Stopwatch.StartNew();
        var r = BeamClustering.Cluster(beams, Tol, lengthTolerance, 24);
        sw.Stop();
        Report($"Timing {count} (length tolerance {lengthTolerance:0})", beams, r, sw.ElapsedMilliseconds);
        int wrong = Enumerable.Range(0, beams.Count).Count(i => r.BlankIndex[i] != r.BlankIndex[family.IndexOf(family[i])]);
        Check(r.Clusters.Count == families, $"timing: expected {families} blanks, got {r.Clusters.Count}");
        Check(wrong == 0, $"timing: {wrong} beams away from their family");
    }
}
