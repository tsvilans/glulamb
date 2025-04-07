using GluLamb.Projects.HHDAC22;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GluLamb.Cix.Operations;

namespace GluLamb.Cix
{
    public class CixWorkpiece : ITransformable, ICix
    {
        /// <summary>
        /// The name of the workpiece.
        /// </summary>
        public string Name;

        /// <summary>
        /// Optional comments to add to top of CIX file.
        /// </summary>
        public List<string> Comments;

        /// <summary>
        /// The world-space coordinate system of the workpiece.
        /// </summary>
        public Plane Plane = Plane.WorldXY;

        /// <summary>
        /// The local origin point of the workpiece, relative to its Plane.
        /// </summary>
        public Point3d LocalOrigin = Point3d.Origin;

        /// <summary>
        /// The blank object of the workpiece.
        /// </summary>
        public CixBlank Blank = null;

        /// <summary>
        /// The shape definition of the body of the workpiece.
        /// </summary>
        public CixShape Shape = null;

        /// <summary>
        /// The workpiece sides for organizing operations.
        /// </summary>
        public BeamSide[] Sides;

        public BeamSide E1 { get { return Sides[0]; } }
        public BeamSide E2 { get { return Sides[1]; } }
        public BeamSide Top { get { return Sides[2]; } }
        public BeamSide Bottom { get { return Sides[3]; } }
        public BeamSide Inside { get { return Sides[4]; } }
        public BeamSide Outside { get { return Sides[5]; } }

        public CixWorkpiece(string name = "Workpiece", Plane plane = default)
        {
            Name = name;
            Plane = plane.IsValid ? plane : Plane.WorldXY;
            Sides = new BeamSide[]
            {
                new BeamSide(BeamSideType.End1),
                new BeamSide(BeamSideType.End2),
                new BeamSide(BeamSideType.Top),
                new BeamSide(BeamSideType.Bottom),
                new BeamSide(BeamSideType.Inside),
                new BeamSide(BeamSideType.Outside)
            };

        }

        /// <summary>
        /// Output the workpiece to CIX variables.
        /// </summary>
        /// <param name="cix"></param>
        /// <param name="prefix"></param>
        public void ToCix(List<string> cix, string prefix = "")
        {
            // Write header
            var dt = System.DateTime.Now;

            cix.Add($"({Name})");
            if (Comments != null)
            {
                foreach (var comment in Comments)
                {
                    if (!string.IsNullOrEmpty(comment))
                        cix.Add($"({Comments})");
                }
            }

            cix.Add($"({dt.Year:0000}-{dt.Month:00}-{dt.Day:00} {dt.Hour:00}:{dt.Minute:00}:{dt.Second:00})");
            cix.Add($"BEGIN PUBLICVARS");

            cix.Add($"{prefix}ORIGO_X={LocalOrigin.X}");
            cix.Add($"{prefix}ORIGO_Y={LocalOrigin.Y}");

            // Write blank
            if (Blank != null)
                Blank.ToCix(cix, prefix);

            // Write shape
            if (Shape != null)
                Shape.ToCix(cix, prefix);

            // Write operations
            for (int i = 0; i < Sides.Length; ++i)
                Sides[i].ToCix(cix, prefix);

            // Write footer
            cix.Add($"(Generated with GluLamb by Tom Svilans)");
            cix.Add($"END PUBLICVARS");
        }

        /// <summary>
        /// Transform the entire workpiece and operations.
        /// </summary>
        /// <param name="xform"></param>
        public void Transform(Transform xform)
        {
            Plane.Transform(xform);

            if (Blank != null) Blank.Transform(xform);
            if (Shape != null) Shape.Transform(xform);

            for (int i = 0; i < Sides.Length; ++i)
                Sides[i].Transform(xform);
        }

        /// <summary>
        /// Flip workpiece sides.
        /// </summary>
        public void FlipSides()
        {
            var temp_sides = new BeamSide[6];
            Array.Copy(Sides, temp_sides, 6);

            Sides[0] = temp_sides[1];
            Sides[1] = temp_sides[0];

            Sides[2] = temp_sides[3];
            Sides[3] = temp_sides[2];

            Sides[0].SideType = BeamSideType.End1;
            Sides[1].SideType = BeamSideType.End2;
            Sides[2].SideType = BeamSideType.Top;
            Sides[3].SideType = BeamSideType.Bottom;
        }

        /// <summary>
        /// Get all operations on workpiece.
        /// </summary>
        /// <returns></returns>
        public List<Operation> GetAllOperations()
        {
            return Sides.SelectMany(x => x.Operations).ToList();
        }

        /// <summary>
        /// Count and appropriately label operations for each side.
        /// </summary>
        /// <param name="wp"></param>
        public static void OrganizeOperations(CixWorkpiece wp)
        {
            foreach (var side in wp.Sides)
            {
                int endCutId = 1;
                int drillGroupId = 1;
                int drillGroup2Id = 1;
                int cleanCutId = 1;
                int plateDowelId = 1;
                int slotId = 1;
                int slotRoughId = 1;
                int lineMachId = 1;
                int slotCutId = 1;
                int cutoutId = 1;

                for (int i = 0; i < side.Operations.Count; ++i)
                {
                    var op = side.Operations[i];

                    if (op is EndCut)
                    {
                        op.Id = endCutId; endCutId++;
                    }
                    else if (op is DrillGroup)
                    {
                        op.Id = drillGroupId; drillGroupId++;
                    }
                    else if (op is DrillGroup2)
                    {
                        op.Id = drillGroup2Id; drillGroup2Id++;
                    }
                    else if (op is CleanCut)
                    {
                        op.Id = cleanCutId; cleanCutId++;
                    }
                    else if (op is SlotMachining)
                    {
                        if ((op as SlotMachining).Rough)
                        {
                            op.Id = slotRoughId; slotRoughId++;
                        }
                        else
                        {
                            op.Id = slotId; slotId++;
                        }
                    }
                    else if (op is SideDrillGroup)
                    {
                        op.Id = plateDowelId; plateDowelId++;
                    }
                    else if (op is LineMachining)
                    {
                        op.Id = lineMachId; lineMachId++;
                    }
                    else if (op is SlotCut)
                    {
                        op.Id = slotCutId; slotCutId++;
                    }
                    else if (op is CrossJointCutout)
                    {
                        op.Id = cutoutId; cutoutId++;
                    }
                }
            }
        }

        /// <summary>
        /// Helper function to make clean cuts on curved blanks.
        /// </summary>
        public void AddCleanCuts()
        {
            if (Blank != null && Blank is CixCurvedBlank curvedBlank)
            {
                E1.Operations.Add(new CleanCut("CleanCut1") { CutLine = curvedBlank.End1 });
                E2.Operations.Add(new CleanCut("CleanCut2") { CutLine = curvedBlank.End2 });
            }
        }

        public static CixWorkpiece CreateWorkpiece(string name = "CixWorkpiece", Plane plane = default)
        {
            var workpiece = new CixWorkpiece(name, plane);

            workpiece.E1.Operations.Add(new CleanCut() { Enabled = false, Id = 1, Name = "Default CleanCut E1" });
            workpiece.E2.Operations.Add(new CleanCut() { Enabled = false, Id = 1, Name = "Default CleanCut E2" });

            workpiece.E1.Operations.Add(new EndCut() { Enabled = false, Id = 1, Name = "Default EndCut E1-1" });
            workpiece.E1.Operations.Add(new EndCut() { Enabled = false, Id = 2, Name = "Default EndCut E1-2" });

            workpiece.E2.Operations.Add(new EndCut() { Enabled = false, Id = 1, Name = "Default EndCut E2-1" });
            workpiece.E2.Operations.Add(new EndCut() { Enabled = false, Id = 2, Name = "Default EndCut E2-2" });

            workpiece.E1.Operations.Add(new DrillGroup() { Enabled = false, Id = 1, Name = "Default Drilling E1" });
            workpiece.E2.Operations.Add(new DrillGroup() { Enabled = false, Id = 1, Name = "Default Drilling E2" });

            workpiece.Top.Operations.Add(new DrillGroup() { Enabled = false, Id = 1, Name = "Default Drilling Top 1" });
            workpiece.Top.Operations.Add(new DrillGroup() { Enabled = false, Id = 2, Name = "Default Drilling Top 2" });
            workpiece.Top.Operations.Add(new DrillGroup() { Enabled = false, Id = 3, Name = "Default Drilling Top 3" });
            workpiece.Top.Operations.Add(new DrillGroup() { Enabled = false, Id = 4, Name = "Default Drilling Top 4" });

            workpiece.Outside.Operations.Add(new DrillGroup() { Enabled = false, Id = 1, Name = "Default Drilling Out 1" });
            workpiece.Outside.Operations.Add(new DrillGroup() { Enabled = false, Id = 2, Name = "Default Drilling Out 2" });
            workpiece.Outside.Operations.Add(new DrillGroup() { Enabled = false, Id = 3, Name = "Default Drilling Out 3" });

            workpiece.Inside.Operations.Add(new DrillGroup() { Enabled = false, Id = 1, Name = "Default Drilling In 1" });
            workpiece.Inside.Operations.Add(new DrillGroup() { Enabled = false, Id = 2, Name = "Default Drilling In 2" });
            workpiece.Inside.Operations.Add(new DrillGroup() { Enabled = false, Id = 3, Name = "Default Drilling In 3" });

            workpiece.Inside.Operations.Add(new CrossJointCutout() { Enabled = false, Id = 1, Name = "Default Cross Cutout In 1" });
            workpiece.Outside.Operations.Add(new CrossJointCutout() { Enabled = false, Id = 1, Name = "Default Cross Cutout Out 1" });

            workpiece.E1.Operations.Add(new SlotMachining() { Enabled = false, Id = 1, Name = "Default Slot Cut E1" });
            workpiece.E1.Operations.Add(new SlotMachining() { Enabled = false, Id = 1, Name = "Default Slot Cut E1 Rough", Rough = true });

            workpiece.E2.Operations.Add(new SlotMachining() { Enabled = false, Id = 1, Name = "Default Slot Cut E2" });
            workpiece.E2.Operations.Add(new SlotMachining() { Enabled = false, Id = 1, Name = "Default Slot Cut E2 Rough", Rough = true });

            workpiece.E1.Operations.Add(new LineMachining() { Enabled = false, Id = 1, Name = "Default Line Cut E1-1" });
            workpiece.E1.Operations.Add(new LineMachining() { Enabled = false, Id = 2, Name = "Default Line Cut E1-2" });
            workpiece.E2.Operations.Add(new LineMachining() { Enabled = false, Id = 1, Name = "Default Line Cut E2-1" });
            workpiece.E2.Operations.Add(new LineMachining() { Enabled = false, Id = 2, Name = "Default Line Cut E2-2" });

            workpiece.E1.Operations.Add(new TenonMachining() { Enabled = false, Id = 1, Name = "Default Tenon E1" });
            workpiece.E2.Operations.Add(new TenonMachining() { Enabled = false, Id = 1, Name = "Default Tenon E2" });

            return workpiece;
        }
    }

}
