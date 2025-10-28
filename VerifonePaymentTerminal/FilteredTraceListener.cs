namespace VerifonePaymentTerminal
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    internal class FilteredTraceListener : TraceListener
    {
        private readonly IEnumerable<string> _filterKeywords;

        private bool ShouldLog(string message)
        {

            return message != null
                && _filterKeywords.Any(o => message.Contains(o)); ;
        }

        public FilteredTraceListener(IEnumerable<string> filterKeywords)
        {
            _filterKeywords = filterKeywords;
        }

        public override void Write(string message)
        {
            if (ShouldLog(message))
            {
                Trace.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
            }
        }

        public override void WriteLine(string message)
        {
            if (ShouldLog(message))
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
            }
        }
    }

}
