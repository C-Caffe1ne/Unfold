using Unfold.Core;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text.Json;

var report = args[0]; var items = new List<object>();
var cases = new List<(string Name, byte[] Bytes)> {
    ("synthetic-static", Synthetic.Fixture(false)),
    ("synthetic-skin-linear", Synthetic.Fixture(true)),
    ("synthetic-skin-morph", Synthetic.Fixture(true,true)),
    ("synthetic-step-morph", Synthetic.Fixture(true,true,"STEP")),
    ("synthetic-dense-linear", Synthetic.DenseFixture()),
    ("synthetic-dense-cubic", Synthetic.DenseFixture(3000,"CUBICSPLINE")) };
foreach(var item in cases) Measure(item.Name,item.Bytes,true);
if(args.Length>1)
foreach(var path in Directory.EnumerateFiles(args[1],"*.glb",SearchOption.AllDirectories).Order())
{
    Measure(Path.GetRelativePath(args[1],path),File.ReadAllBytes(path),false);
    if(items.Count%25==0)Console.WriteLine(items.Count+" models processed");
}
File.WriteAllText(report,JsonSerializer.Serialize(new {coreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(GlbModel).Assembly.Location))),framework=RuntimeInformation.FrameworkDescription,os=RuntimeInformation.OSDescription,items},new JsonSerializerOptions{WriteIndented=true}));
void Measure(string name,byte[] bytes,bool synthetic)
{
    try
    {
        var allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();var model=GlbModel.Parse(bytes);
        var loadMs=watch.Elapsed.TotalMilliseconds;var loadBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        var clips=new[]{model.Animations[0],model.Animations[model.Animations.Count/2],model.Animations[^1]}.Distinct().ToArray();
        var frames=new List<object>();
        foreach(var info in clips)
        {
            var clip=model.CreateAnimation(info.Name,new("model.glb"),model.Animations[0].Name);
            foreach(var size in synthetic?new[]{192,384,576}:new[]{192})
            foreach(var index in new[]{0,clip.Count/2,clip.Count-1}.Distinct())
            {
                var pixels=clip.GetFrame(index,size).Image.Pixels;
                frames.Add(new{clip=info.Name,index,size,sha=Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(pixels.AsSpan())))});
            }
        }
        var performance=new List<object>();
        if(synthetic)
        {
            var clip=model.CreateAnimation(model.Animations[0].Name,new("model.glb"),model.Animations[0].Name);
            foreach(var size in new[]{192,576,1024})
            {
                for(var i=1;i<5;i++)clip.GetFrame(i%clip.Count,size);
                allocated=GC.GetAllocatedBytesForCurrentThread();watch.Restart();
                for(var i=1;i<=40;i++)clip.GetFrame(i%clip.Count,size);
                performance.Add(new{size,allocatedPerFrame=(GC.GetAllocatedBytesForCurrentThread()-allocated)/40,millisecondsPerFrame=watch.Elapsed.TotalMilliseconds/40});
            }
        }
        items.Add(new{name,success=true,fileBytes=bytes.Length,loadBytes,loadMs,model.TriangleCount,animationCount=model.Animations.Count,frames,performance});
    }
    catch(Exception e){items.Add(new{name,success=false,error=e.Message});}
}
