using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace VerifonePaymentTerminal
{
public partial class MainWindow
    {
        private class FilteredTraceListener : TraceListener
        {
            public FilteredTraceListener(IEnumerable<string> filterKeywords, IPortTraceMessages portTraceMessages)
            {
                _filterKeywords = filterKeywords;
                _portTraceMessages = portTraceMessages;
            }

            private readonly IEnumerable<string> _filterKeywords;
            private readonly IPortTraceMessages _portTraceMessages;

            private bool ShouldLog(string message)
            {

                return message != null
                    && _filterKeywords.Any(o => message.Contains(o)); ;
            }

            public override void Write(string message)
            {
                if (ShouldLog(message))
                {
                    _portTraceMessages.AddPortTraceMessage($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
                }
            }

            public override void WriteLine(string message)
            {
                if (ShouldLog(message))
                {
                    _portTraceMessages.AddPortTraceMessage($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
                }
            }
        }
    }
}