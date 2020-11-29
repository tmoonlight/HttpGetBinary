using System;
using System.Threading;

namespace HttpGetBinary.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var httpServer = new HttpServer();
            CancellationTokenSource cts = new CancellationTokenSource();

            httpServer.StartHttpService(cts,50888);
            Console.ReadLine();

        }
    }
}
