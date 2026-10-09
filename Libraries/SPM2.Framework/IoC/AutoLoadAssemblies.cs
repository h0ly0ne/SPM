using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autofac;
using System.Diagnostics;
using System.Reflection;
using System.IO;

namespace SPM2.Framework.IoC
{
    public class AutoLoadAssemblies : Autofac.Module
    {

        public IEnumerable<Assembly> Assemblies { get; set; }

        public AutoLoadAssemblies()
        {
            // Auto find all assemblies
            Assemblies = FindAssemblies("addin");
        }

        public AutoLoadAssemblies(IEnumerable<Assembly> assemblies)
        {
            Assemblies = assemblies;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.Register(c => new AutofacIocAdapter(c.Resolve<IComponentContext>())).As<IContainerAdapter>();

            foreach (var assembly in Assemblies)
            {
                var types = assembly.GetTypes();

                foreach (var t in types)
                {
                    //Console.WriteLine("Trying Type: " + t.Name);

                    if (!t.IsClass ||
                        t.IsAbstract ||
                        t.IsGenericTypeDefinition ||
                        t.IsDelegate() ||
                        t.HasIoCIgnore())
                        continue;


                    builder.RegisterType(t).AsSelf().AsImplementedInterfaces().IoC();

#if DEBUG
                    Console.WriteLine("Type: " + t.Name);
#endif
                }

            }
        }


        private IEnumerable<Assembly> FindAssemblies(string addinPath)
        {
            //A catalog that can aggregate other catalogs
            //var aggrCatalog = new AggregateCatalog();

            //An assembly catalog
            //var currentAssemblyCatalog = new AssemblyCatalog(Assembly.GetExecutingAssembly());
            //aggrCatalog.Catalogs.Add(currentAssemblyCatalog);
            
            var list = new List<Assembly>();
            //var executionAssembly = Assembly.GetExecutingAssembly();
            string assmPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var dir = new DirectoryInfo(assmPath);

            foreach (var item in dir.GetFiles("*.dll"))
            {
                if (IsExcluded(item))
                    continue;

                TryLoad(list, item);
            }

            foreach (var item in dir.GetFiles("*.exe"))
            {
                TryLoad(list, item);
            }

            //dir.GetFiles(
            //executionAssembly.CodeBase

            ////var dirCatalog = new DirectoryCatalog(assmPath + "\\" + addinPath, "*.dll");

            //aggrCatalog.Catalogs.Add(new DirectoryCatalog(assmPath, "*.dll"));
            //aggrCatalog.Catalogs.Add(new DirectoryCatalog(assmPath, "*.exe"));

            //Create a container
            return list;
        }

        /// <summary>
        /// Loads a file as a managed assembly, skipping anything that is not one.
        ///
        /// This directory is scanned blindly for *.dll, but native dependencies live here
        /// too - WebView2Loader.dll, for example. Assembly.LoadFile throws
        /// BadImageFormatException for those, and because this runs from the
        /// AutoLoadAssemblies constructor the exception used to escape all the way to
        /// Program.Main, which swallowed it and exited silently with no window and no
        /// event log entry.
        /// </summary>
        private static void TryLoad(List<Assembly> list, FileInfo file)
        {
            try
            {
                list.Add(Assembly.LoadFile(file.FullName));
            }
            catch (BadImageFormatException)
            {
                // Native DLL, or built for a different architecture. Not our concern.
                Trace.WriteLine("Skipped non-managed file: " + file.Name);
            }
            catch (FileLoadException ex)
            {
                Trace.WriteLine("Could not load assembly " + file.Name + ": " + ex.Message);
            }
        }

        private bool IsExcluded(FileInfo file)
        {
            return "AutoFac.dll".StartsWith(file.Name, StringComparison.OrdinalIgnoreCase);


        }

    }
}

