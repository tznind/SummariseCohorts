using CommandLine;
using FAnsi.Discovery;
using NPOI.SS.Formula.Functions;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.Curation.Data.Cohort;
using Rdmp.Core.Providers;
using Rdmp.Core.Repositories;
using Rdmp.Core.Startup;
using ReusableLibraryCode;
using System;
using System.IO;
using System.Linq;

namespace cic
{
    class Program
    {
        
        public class Options
        {
            [Option('s', "server", Required = true, HelpText = "RDMP Catalogue server")]
            public string Server { get; set; }

            [Option('d', "database", Required = true, HelpText = "RDMP Catalogue database")]
            public string Database { get; set; }
            
            [Option('o', "out", Required = true, HelpText = "Directory to write results into")]
            public string Out {get;set;}
        }
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                       var db = new DiscoveredServer(o.Server,o.Database,FAnsi.DatabaseType.MicrosoftSQLServer,null,null);
                       var repo = new LinkedRepositoryProvider(db.Builder.ConnectionString,null);

                       var cp = new CatalogueChildProvider(repo.CatalogueRepository,null,null);

                       var dir = new DirectoryInfo(o.Out);
                       dir.Create();

                       foreach(var cic in cp.AllCohortIdentificationConfigurations)
                       {
                            var fi = new FileInfo(Path.Combine(dir.FullName,UsefulStuff.RemoveIllegalFilenameCharacters(cic.Name) +".txt"));

                           using(var fs = new StreamWriter(fi.Create()))
                           {
                               WriteOut(cic,fs);
                           }

                       }

                   });
        }

        private static void WriteOut(CohortIdentificationConfiguration cic, StreamWriter fs)
        {
            fs.WriteLine(cic.Description);
            WriteOut(cic.RootCohortAggregateContainer,fs,1);

            var joinables = cic.GetAllJoinables();
            fs.WriteLine("PATIENT INDEX TABLES:");

            if(joinables.Length == 0)
                fs.WriteLine("None");
            else
            foreach(var joinable in joinables)
                WriteOut(joinable.AggregateConfiguration,fs,0);
        }

        private static void WriteOut(CohortAggregateContainer container, StreamWriter fs,int tabs)
        {
            if(container == null)
                return;

            var contents = container.GetOrderedContents().ToArray();

            if(contents.Length == 0)
                return;

            if(contents.Length == 1 && (container.Name.Equals("UNION")||container.Name.Equals("INTERSECT")||container.Name.Equals("EXCEPT")))
            {
                //container has only one thing in it which makes it kinda pointless so snip it out like the query builder would                
                if(contents[0] is CohortAggregateContainer subContainer)
                    WriteOut(subContainer,fs,tabs);

                if(contents[0] is AggregateConfiguration ac)
                    WriteOut(ac,fs,tabs);

                return;
            }

            fs.WriteLine(GetTabs(tabs) + container.Name);

            foreach(var sub in contents)
            {
                if(sub is CohortAggregateContainer subContainer)
                    WriteOut(subContainer,fs,tabs+1);

                if(sub is AggregateConfiguration ac)
                    WriteOut(ac,fs,tabs+1);
            }

        }

        private static void WriteOut(AggregateConfiguration ac, StreamWriter fs, int tabs)
        {
            fs.WriteLine(GetTabs(tabs) + ac.Name);

            WriteOut(ac.RootFilterContainer,fs,tabs+1);
        }

        private static void WriteOut(IContainer filterContainer, StreamWriter fs, int tabs)
        {
            if(filterContainer == null)
                return;

            fs.WriteLine(GetTabs(tabs) + filterContainer.Operation);


            foreach(var sub in filterContainer.GetSubContainers())
                WriteOut(sub,fs,tabs+1);

            foreach(var filter in filterContainer.GetFilters())
                WriteOut(filter,fs,tabs+1);
        }

        private static void WriteOut(IFilter filter, StreamWriter fs, int tabs)
        {
            fs.WriteLine(GetTabs(tabs) + filter.Name);
        }

        private static string GetTabs(int tabs)
        {
            return new string('\t',tabs);
        }
    }
}
