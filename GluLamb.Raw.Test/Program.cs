// See https://aka.ms/new-console-template for more information
using GluLamb.Raw;

Console.WriteLine("Hello, World!");

var verts = new float[]
{
    0,0,0,
    1,1,0,
    0,1,0,
    1,0,1
};

var faces = new int[]
{
    0, 1, 2,
    1, 2, 3,
    2, 3, 0,
    3, 0, 1
};


var dc = new DualCon();
dc.Remesh(verts, faces);

Console.WriteLine("New vertices:");
foreach (var v in dc.Output.Vertices)
{
    Console.WriteLine($"{v[0]:0.000} {v[1]:0.000} {v[2]:0.000}");
}

Console.WriteLine("---");
Console.WriteLine("New quads:");
foreach (var f in dc.Output.Quads)
{
    Console.WriteLine($"{f[0]} {f[1]} {f[2]} {f[3]}");
}

Console.ReadLine();
