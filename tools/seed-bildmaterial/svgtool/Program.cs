using System.Drawing;
using System.Drawing.Imaging;
using Svg;

// args: inDir outDir maxSize
var inDir = args[0];
var outDir = args[1];
var maxSeite = args.Length > 2 ? float.Parse(args[2]) : 96f;
Directory.CreateDirectory(outDir);

int ok = 0;
var fehler = new List<string>();

foreach (var file in Directory.GetFiles(inDir))
{
    var code = Path.GetFileNameWithoutExtension(file);
    var ext = Path.GetExtension(file).ToLowerInvariant();
    var outPath = Path.Combine(outDir, code + ".png");
    try
    {
        if (ext == ".svg")
        {
            var doc = SvgDocument.Open<SvgDocument>(file);
            var breite = doc.Width.Value;
            var hoehe = doc.Height.Value;
            if (breite <= 0 || hoehe <= 0) { breite = 100; hoehe = 100; }
            if (doc.ViewBox.Width <= 0 || doc.ViewBox.Height <= 0)
                doc.ViewBox = new Svg.SvgViewBox(0, 0, breite, hoehe);
            var scale = maxSeite / Math.Max(breite, hoehe);
            var zielB = Math.Max(1, (int)(breite * scale));
            var zielH = Math.Max(1, (int)(hoehe * scale));
            doc.Width = zielB;
            doc.Height = zielH;
            using var bmp = doc.Draw(zielB, zielH);
            bmp.Save(outPath, ImageFormat.Png);
        }
        else
        {
            using var src = Image.FromFile(file);
            var scale = maxSeite / Math.Max(src.Width, src.Height);
            var zielB = Math.Max(1, (int)(src.Width * scale));
            var zielH = Math.Max(1, (int)(src.Height * scale));
            using var bmp = new Bitmap(zielB, zielH);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, zielB, zielH);
            }
            bmp.Save(outPath, ImageFormat.Png);
        }
        ok++;
    }
    catch (Exception ex)
    {
        fehler.Add($"{code}: {ex.Message}");
    }
}

Console.WriteLine($"OK: {ok}, Fehler: {fehler.Count}");
foreach (var f in fehler) Console.WriteLine("  " + f);
