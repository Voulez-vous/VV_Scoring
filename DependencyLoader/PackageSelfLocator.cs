using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using Assembly = System.Reflection.Assembly;

namespace VV.DependencyLoader
{
    public class PackageSelfLocator
    {
        public static string GetCurrentPackagePath<T>()
        {
            Assembly asm = typeof(T).Assembly;
            string asmdefPath =
                CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(
                    asm.GetName().Name);

            if (string.IsNullOrEmpty(asmdefPath))
                return null;

            PackageInfo packageInfo = PackageInfo.FindForAssetPath(asmdefPath);
            return packageInfo?.assetPath;
        }
    }
}