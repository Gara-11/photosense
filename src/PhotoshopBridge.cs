using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PixelPatchStudio
{
    internal sealed class PhotoshopConnectionException : InvalidOperationException
    {
        public readonly bool ElevationSuggested;

        public PhotoshopConnectionException(string message, bool elevationSuggested, Exception innerException)
            : base(message, innerException)
        {
            ElevationSuggested = elevationSuggested;
        }
    }

    internal sealed class PhotoshopBridge : IDisposable
    {
        private object application;

        public PhotoshopImage ExportActiveDocument(string outputDirectory)
        {
            EnsureConnected();
            Directory.CreateDirectory(outputDirectory);
            string path = Path.Combine(outputDirectory, "photoshop-source-" + Guid.NewGuid().ToString("N") + ".png");
            string script =
                "#target photoshop\n" +
                "(function(){\n" +
                "if(app.documents.length===0) throw new Error('Photoshop 中没有打开的文档');\n" +
                "var original=app.activeDocument;\n" +
                "var duplicate=original.duplicate('__PixelPatch_Source__');\n" +
                "app.activeDocument=duplicate;\n" +
                "try { if(duplicate.mode!==DocumentMode.RGB) duplicate.changeMode(ChangeMode.RGB); } catch(e){}\n" +
                "try { duplicate.bitsPerChannel=BitsPerChannelType.EIGHT; } catch(e){}\n" +
                "duplicate.flatten();\n" +
                "var options=new PNGSaveOptions(); options.interlaced=false;\n" +
                "duplicate.saveAs(new File('" + Js(path) + "'),options,true,Extension.LOWERCASE);\n" +
                "var info={name:original.name,width:Math.round(original.width.as('px')),height:Math.round(original.height.as('px')),id:original.id,resolution:original.resolution};\n" +
                "duplicate.close(SaveOptions.DONOTSAVECHANGES); app.activeDocument=original;\n" +
                "return info.name+'\\n'+info.width+'\\n'+info.height+'\\n'+info.id+'\\n'+info.resolution;\n" +
                "})();";
            string result = Convert.ToString(RunScript(script), CultureInfo.InvariantCulture);
            string[] parts = (result ?? "").Split(new[] { '\n' }, StringSplitOptions.None);
            if (!File.Exists(path)) throw new IOException("Photoshop 没有生成临时图片。请确认文档不是受保护状态。");
            using (Bitmap bitmap = new Bitmap(path))
            {
                return new PhotoshopImage
                {
                    ImagePath = path,
                    DocumentName = parts.Length > 0 && parts[0].Length > 0 ? parts[0] : "Photoshop 文档",
                    Width = bitmap.Width,
                    Height = bitmap.Height,
                    DocumentId = parts.Length > 3 ? ParseInt(parts[3]) : 0,
                    Resolution = parts.Length > 4 ? ParseDouble(parts[4]) : bitmap.HorizontalResolution
                };
            }
        }

        public string ExportAutomaticMask(string outputDirectory, int documentId, bool background)
        {
            EnsureConnected();
            Directory.CreateDirectory(outputDirectory);
            string path = Path.Combine(outputDirectory, "photoshop-mask-" + Guid.NewGuid().ToString("N") + ".png");
            string invert = background ? "duplicate.selection.invert();\n" : "";
            string script =
                "#target photoshop\n" +
                "(function(){\n" +
                "if(app.documents.length===0) throw new Error('Photoshop 中没有打开的文档');\n" +
                "var original=null; for(var i=0;i<app.documents.length;i++){if(app.documents[i].id===" + documentId + "){original=app.documents[i];break;}}\n" +
                "if(!original) throw new Error('最初获取的 Photoshop 文档已关闭，请重新获取');\n" +
                "app.activeDocument=original; var duplicate=original.duplicate('__PixelPatch_Mask__'); app.activeDocument=duplicate;\n" +
                "try { if(duplicate.mode!==DocumentMode.RGB) duplicate.changeMode(ChangeMode.RGB); } catch(e){}\n" +
                "try { duplicate.bitsPerChannel=BitsPerChannelType.EIGHT; } catch(e){}\n" +
                "duplicate.flatten();\n" +
                "var command=stringIDToTypeID('autoCutout'); var descriptor=new ActionDescriptor();\n" +
                "descriptor.putBoolean(stringIDToTypeID('sampleAllLayers'),false); executeAction(command,descriptor,DialogModes.NO);\n" +
                invert +
                "var white=new SolidColor(); white.rgb.red=255; white.rgb.green=255; white.rgb.blue=255;\n" +
                "var black=new SolidColor(); black.rgb.red=0; black.rgb.green=0; black.rgb.blue=0;\n" +
                "duplicate.selection.fill(white,ColorBlendMode.NORMAL,100,false); duplicate.selection.invert();\n" +
                "duplicate.selection.fill(black,ColorBlendMode.NORMAL,100,false); duplicate.selection.deselect(); duplicate.flatten();\n" +
                "var options=new PNGSaveOptions(); options.interlaced=false; duplicate.saveAs(new File('" + Js(path) + "'),options,true,Extension.LOWERCASE);\n" +
                "duplicate.close(SaveOptions.DONOTSAVECHANGES); app.activeDocument=original; return 'ok';\n" +
                "})();";
            RunScript(script);
            if (!File.Exists(path)) throw new IOException("Photoshop 自动选区未生成蒙版。");
            return path;
        }

        public void PlacePatchAsLayer(string patchPath, string layerName, int documentId)
        {
            EnsureConnected();
            if (!File.Exists(patchPath)) throw new FileNotFoundException("找不到待回写的补丁图片。", patchPath);
            string result = Convert.ToString(RunScript(BuildPlacePatchScript(patchPath, layerName, documentId)), CultureInfo.InvariantCulture);
            const string errorMarker = "__PIXELPATCH_PLACE_ERROR__";
            if (!string.IsNullOrEmpty(result) && result.StartsWith(errorMarker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(result.Substring(errorMarker.Length).TrimStart('\r', '\n'));
            }
        }

        internal static string BuildPlacePatchScript(string patchPath, string layerName, int documentId)
        {
            return
                "#target photoshop\n" +
                "(function(){\n" +
                "if(app.documents.length===0) throw new Error('Photoshop 中没有打开的目标文档');\n" +
                "var target=null; for(var i=0;i<app.documents.length;i++){if(app.documents[i].id===" + documentId + "){target=app.documents[i];break;}}\n" +
                "if(!target) throw new Error('最初获取的 Photoshop 文档已关闭，请重新获取');\n" +
                "var sourceFile=new File('" + Js(patchPath) + "');\n" +
                "function placePatch(linked){ app.activeDocument=target; var place=charIDToTypeID('Plc '); var descriptor=new ActionDescriptor(); descriptor.putPath(charIDToTypeID('null'),sourceFile);\n" +
                "descriptor.putEnumerated(charIDToTypeID('FTcs'),charIDToTypeID('QCSt'),charIDToTypeID('Qcsa'));\n" +
                "var offset=new ActionDescriptor(); offset.putUnitDouble(charIDToTypeID('Hrzn'),charIDToTypeID('#Pxl'),0); offset.putUnitDouble(charIDToTypeID('Vrtc'),charIDToTypeID('#Pxl'),0); descriptor.putObject(charIDToTypeID('Ofst'),charIDToTypeID('Ofst'),offset);\n" +
                "if(linked) descriptor.putBoolean(stringIDToTypeID('linked'),true); executeAction(place,descriptor,DialogModes.NO); var layer=target.activeLayer; layer.name='" + Js(layerName) + "'; return layer.name; }\n" +
                "try { return 'linked\\n'+placePatch(true); } catch(linkedError) {\n" +
                "try { return 'embedded\\n'+placePatch(false); } catch(embeddedError) {\n" +
                "return '__PIXELPATCH_PLACE_ERROR__\\n链接置入失败：'+linkedError.message+'（行 '+linkedError.line+'）\\n嵌入置入失败：'+embeddedError.message+'（行 '+embeddedError.line+'）'; } }\n" +
                "})();";
        }

        public void PlacePatchTilesAsGroup(IList<PatchTile> tiles, string groupName, int documentId)
        {
            EnsureConnected();
            if (tiles == null || tiles.Count == 0) throw new InvalidOperationException("没有可回写的补丁分块。");
            string result = Convert.ToString(RunScript(BuildPlaceTilesScript(tiles, groupName, documentId)), CultureInfo.InvariantCulture);
            const string errorMarker = "__PIXELPATCH_TILE_ERROR__";
            if (!string.IsNullOrEmpty(result) && result.StartsWith(errorMarker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(result.Substring(errorMarker.Length).TrimStart('\r', '\n'));
            }
        }

        internal static string BuildPlaceTilesScript(IList<PatchTile> tiles, string groupName, int documentId)
        {
            StringBuilder script = new StringBuilder();
            script.Append("#target photoshop\n(function(){\n");
            script.Append("if(app.documents.length===0) return '__PIXELPATCH_TILE_ERROR__\\nPhotoshop 中没有打开的目标文档';\n");
            script.Append("var target=null; for(var i=0;i<app.documents.length;i++){if(app.documents[i].id===").Append(documentId).Append("){target=app.documents[i];break;}}\n");
            script.Append("if(!target) return '__PIXELPATCH_TILE_ERROR__\\n最初获取的 Photoshop 文档已关闭，请重新获取';\n");
            script.Append("app.activeDocument=target; var group=target.layerSets.add(); group.name='").Append(Js(groupName)).Append("';\n");
            script.Append("function addTile(path,x,y,w,h,name){ app.activeDocument=target; var descriptor=new ActionDescriptor(); descriptor.putPath(charIDToTypeID('null'),new File(path)); descriptor.putEnumerated(charIDToTypeID('FTcs'),charIDToTypeID('QCSt'),charIDToTypeID('Qcsa')); var offset=new ActionDescriptor(); offset.putUnitDouble(charIDToTypeID('Hrzn'),charIDToTypeID('#Pxl'),x+w/2-target.width.as('px')/2); offset.putUnitDouble(charIDToTypeID('Vrtc'),charIDToTypeID('#Pxl'),y+h/2-target.height.as('px')/2); descriptor.putObject(charIDToTypeID('Ofst'),charIDToTypeID('Ofst'),offset); descriptor.putBoolean(stringIDToTypeID('linked'),true); executeAction(charIDToTypeID('Plc '),descriptor,DialogModes.NO); var layer=target.activeLayer; layer.name=name; layer.move(group,ElementPlacement.INSIDE); }\n");
            script.Append("try {\n");
            for (int i = 0; i < tiles.Count; i++)
            {
                PatchTile tile = tiles[i];
                script.Append("addTile('").Append(Js(tile.ImagePath)).Append("',")
                    .Append(tile.X).Append(',').Append(tile.Y).Append(',').Append(tile.Width).Append(',').Append(tile.Height)
                    .Append(",'补丁 ").Append(i + 1).Append("/" + tiles.Count).Append("');\n");
            }
            script.Append("return 'tiles\\n").Append(tiles.Count).Append("';\n");
            script.Append("} catch(e) { try { group.remove(); } catch(removeError){} return '__PIXELPATCH_TILE_ERROR__\\n分块置入失败：'+e.message+'（行 '+e.line+'）'; }\n})();");
            return script.ToString();
        }

        public void Dispose()
        {
            if (application != null && Marshal.IsComObject(application))
            {
                try { Marshal.FinalReleaseComObject(application); } catch { }
            }
            application = null;
        }

        private void EnsureConnected()
        {
            if (application != null) return;
            Type type = Type.GetTypeFromProgID("Photoshop.Application");
            if (type == null) throw new InvalidOperationException("没有检测到 Photoshop。请先安装并启动 Photoshop。");

            Exception lastError = null;
            try
            {
                application = Marshal.GetActiveObject("Photoshop.Application");
            }
            catch (Exception ex) { lastError = ex; }

            if (application == null && PhotoshopIsRunningAtHigherIntegrity())
            {
                throw new PhotoshopConnectionException(
                    "Photoshop 正在以管理员权限运行，而 PhotoSense 当前是普通权限，Windows 阻止了两者连接。软件可以自动以相同权限重启；无需关闭 Photoshop。",
                    true,
                    lastError);
            }

            for (int attempt = 0; application == null && attempt < 2; attempt++)
            {
                try
                {
                    application = Activator.CreateInstance(type);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < 1) Thread.Sleep(350);
                }
            }

            if (application != null) return;
            bool elevationSuggested = HasHResult(lastError, unchecked((int)0x80080005)) || HasHResult(lastError, unchecked((int)0x80070005));
            string message = elevationSuggested
                ? "Photoshop 正在以管理员权限运行，而 PhotoSense 当前是普通权限，Windows 阻止了两者连接。软件可以自动以相同权限重启；无需关闭 Photoshop。"
                : "无法连接 Photoshop。请确认 Photoshop 已启动且没有停留在启动、更新或崩溃恢复窗口。系统信息：" + (lastError == null ? "未知错误" : lastError.Message);
            throw new PhotoshopConnectionException(message, elevationSuggested, lastError);
        }

        private object RunScript(string script)
        {
            try
            {
                return application.GetType().InvokeMember("DoJavaScript", BindingFlags.InvokeMethod, null, application, new object[] { script, null, 1 }, CultureInfo.InvariantCulture);
            }
            catch (TargetInvocationException ex)
            {
                Exception cause = ex.InnerException ?? ex;
                throw new InvalidOperationException("Photoshop 操作失败：" + cause.Message, cause);
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException("Photoshop 操作失败：" + ex.Message, ex);
            }
        }

        private static string Js(string value)
        {
            return value.Replace("\\", "/").Replace("'", "\\'").Replace("\r", "").Replace("\n", "\\n");
        }

        private static int ParseInt(string value)
        {
            int result;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : 0;
        }

        private static double ParseDouble(string value)
        {
            double result;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && result > 0 ? result : 96d;
        }

        private static bool HasHResult(Exception exception, int hresult)
        {
            Exception current = exception;
            while (current != null)
            {
                if (current.HResult == hresult) return true;
                current = current.InnerException;
            }
            return false;
        }

        private static bool PhotoshopIsRunningAtHigherIntegrity()
        {
            bool accessDenied = false;
            bool accessible = false;
            Process[] processes = Process.GetProcessesByName("Photoshop");
            try
            {
                foreach (Process process in processes)
                {
                    try
                    {
                        ProcessModule module = process.MainModule;
                        if (module != null && !string.IsNullOrEmpty(module.FileName)) accessible = true;
                    }
                    catch (Win32Exception ex)
                    {
                        if (ex.NativeErrorCode == 5) accessDenied = true;
                    }
                    catch { }
                    finally { process.Dispose(); }
                }
            }
            catch { }
            return accessDenied && !accessible;
        }
    }
}
