using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Plastic.Newtonsoft.Json.Linq;
using Unity.Properties;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace VV.DependencyLoader
{
    [InitializeOnLoad]
    public class DependencyLoader
    {
        private static readonly List<DependencyState> _dependencies = new();
        private static PackageCollection _installedPackages;
        private static PackageManifest _packageManifest;
        
        public static IReadOnlyList<DependencyState> Dependencies => _dependencies;
        public static PackageManifest Manifest { get; private set; }
        
        static DependencyLoader()
        {
            EditorApplication.delayCall += async () =>
            {
                await Refresh();
                if (await AllDependenciesInstalled()) return;
                // Open the window only when the dependencies are missing
                DependencyLoaderWindow.Open();
            };
        }

        public static async Task Refresh()
        {
            await LoadInstalledPackages();
            LoadManifest();
        }

        private static async Task LoadInstalledPackages()
        {
            ListRequest request = Client.List();
            while (!request.IsCompleted)
                await Task.Yield();

            if (request.Status == StatusCode.Failure)
            {
                Debug.LogError(request.Error.message);
                return;
            }

            _installedPackages = request.Result;
        }

        private static void LoadManifest()
        {
            string packageRoot =
                PackageSelfLocator.GetCurrentPackagePath<DependencyLoader>();

            string packagePath = Path.Combine(packageRoot, "package.json");

            if (!File.Exists(packagePath))
            {
                Debug.LogError("package.json not found");
                return;
            }
            
            string dependenciesPath = Path.Combine(packageRoot, "dependencies.json");
            
            if (!File.Exists(dependenciesPath))
            {
                Debug.LogError("dependencies.json not found");
                return;
            }
            
            string jsonManifest = File.ReadAllText(packagePath);
            Dictionary<string, string> dependencies = ReadDependencies(jsonManifest);
            
            foreach (KeyValuePair<string, string> dep in dependencies)
            {
                if(_dependencies.Exists(d => d.entry.packageId == dep.Key)) continue;
                _dependencies.Add(new DependencyState
                {
                    entry = new DependencyEntry
                    {
                        packageId = dep.Key,
                        version = dep.Value
                    },
                    status = IsPackageInstalled(dep.Key)
                        ? DependencyStatus.Installed
                        : DependencyStatus.Missing
                });
            }
            
            
            
            string jsonDependencies = File.ReadAllText(dependenciesPath);
            Dictionary<string, string> dependenciesLinks = ReadDependencies(jsonDependencies, "git");

            foreach (DependencyState depState in _dependencies)
            {
                if (dependenciesLinks.TryGetValue(depState.entry.packageId, out string url))
                {
                    depState.entry.url = url;
                }
                else
                {
                    depState.type = DependencyType.UPM;
                }
            }
            
            Manifest = JsonUtility.FromJson<PackageManifest>(jsonManifest);
        }

        private static async Task<bool> AllDependenciesInstalled()
        {
            if (!_installedPackages.Any())
            {
                await LoadInstalledPackages();
            }

            return _dependencies.All(dep => IsPackageInstalled(dep.entry.packageId));
        }

        private static bool IsPackageInstalled(string packageName)
        {
            PackageInfo packageInstalled = _installedPackages.FirstOrDefault(q => q.name == packageName); 
            return packageInstalled != null;
        }
        
        public static Dictionary<string, string> ReadDependencies(string json, string key="dependencies")
        {
            JObject root = JObject.Parse(json);
            var deps = root[key] as JObject;

            var result = new Dictionary<string, string>();

            if (deps == null)
                return result;

            foreach (var prop in deps.Properties())
            {
                result[prop.Name] = prop.Value.ToString();
            }

            return result;
        }

        public static async Task<DependencyStatus> InstallDependency(DependencyState state)
        {
            Debug.Log($"Installing dependency {state.entry.packageId}...");
            if (state.status == DependencyStatus.Installed)
                return DependencyStatus.Pending;

            state.status = DependencyStatus.Installing;
            DependencyLoaderWindow.RepaintIfOpen();

            string identifier = state.type switch
            {
                DependencyType.Git => $"{state.entry.url}",
                DependencyType.UPM => $"{state.entry.packageId}@{state.entry.version}",
                _ => ""
            };

            if (string.IsNullOrEmpty(identifier))
            {
                Debug.LogError($"Dependency {state.entry.packageId} identifier is null or empty");
                state.status = DependencyStatus.Failed;
                return state.status;
            }

            AddRequest request =
                Client.Add(identifier);

            while (!request.IsCompleted)
                await Task.Yield();

            state.status =
                request.Status == StatusCode.Success
                    ? DependencyStatus.Installed
                    : DependencyStatus.Failed;

            await Refresh();
            DependencyLoaderWindow.RepaintIfOpen();
            if(state.status == DependencyStatus.Failed)
                Debug.Log($"Dependency : {state.entry.packageId} - {state.entry.url} - {state.status}");
            return state.status;
        }

        public static async Task InstallAll()
        {
            Debug.Log($"Installing all dependencies...");
            List<DependencyState> dependencies = new(_dependencies);
            foreach (DependencyState dep in dependencies.Where(d => d.status == DependencyStatus.Missing))
            {
                await InstallDependency(dep);
            }
        }
    }

    [EditorWindowTitle(title="Scoring Dependency Loader")]
    public class DependencyLoaderWindow : EditorWindow
    {
        private static DependencyLoaderWindow _instance;
        public static bool IsOpen => _instance != null;

        [MenuItem("Window/VV/Scoring/Dependency Loader")]
        public static void Open()
        {
            if (_instance != null)
            {
                _instance.Repaint();
                return;
            }

            var window = GetWindow<DependencyLoaderWindow>();
            window.titleContent = new GUIContent("Package Dependencies");
            window.ShowUtility();
        }

        public static void RepaintIfOpen()
        {
            _instance?.Repaint();
        }

        private void OnEnable()
        {
            Debug.Log($"Test");
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            _instance = this;
            CreateGUI();
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Close;
            _instance = null;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            
            if(DependencyLoader.Manifest == null) return;

            var title = new Label(DependencyLoader.Manifest?.displayName ?? "Dependencies")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14
                }
            };
            rootVisualElement.Add(title);

            rootVisualElement.Add(new VisualElement { style = { height = 8 } });

            foreach (DependencyState dep in DependencyLoader.Dependencies)
            {
                rootVisualElement.Add(CreateDependencyRow(dep));
            }

            var installAll = new Button(async () =>
            {
                Debug.Log($"Install all");
                await DependencyLoader.InstallAll();
            })
            {
                text = "Install All Dependencies",
                style =
                {
                    marginTop = 10
                }
            };

            rootVisualElement.Add(installAll);
        }

        private VisualElement CreateDependencyRow(DependencyState dep)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 4,
                    marginLeft = 5,
                    marginRight = 5,
                }
            };

            var label = new Label($"{dep.entry.packageId} ({dep.entry.version})")
            {
                style =
                {
                    flexGrow = 1
                }
            };
            
            var statusContainer = new VisualElement
            {
                style =
                {
                    width = 80,
                    justifyContent = Justify.Center
                }
            };

            var statusLabel = new Label(dep.status.ToString());
            var loadingBar = new ProgressBar
            {
                title = "Installing...",
                value = 0
            };

            loadingBar.style.display = DisplayStyle.None;
            
            statusContainer.Add(statusLabel);
            statusContainer.Add(loadingBar);

            var button = new Button(async () =>
            {
                loadingBar.style.display =  DisplayStyle.Flex;
                statusLabel.style.display =  DisplayStyle.None;
                loadingBar.AddToClassList("unity-progress-bar--indeterminate");
                DependencyStatus newDepStatus = await DependencyLoader.InstallDependency(dep);
                loadingBar.style.display =  DisplayStyle.None;
                statusLabel.style.display =  DisplayStyle.Flex;
                loadingBar.RemoveFromClassList("unity-progress-bar--indeterminate");
                statusLabel.text = newDepStatus.ToString();
            })
            {
                text = "Install"
            };

            button.SetEnabled(dep.status is DependencyStatus.Missing or DependencyStatus.Failed);
            
            row.Add(label);
            row.Add(statusContainer);
            row.Add(button);

            return row;
        }
    }
}