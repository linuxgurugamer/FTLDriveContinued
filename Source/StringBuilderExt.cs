using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScienceFoundry.FTL
{
    public static class StringBuilderExt
    {
        public static void AppendEx(this StringBuilder sb, string value)
        {
            sb.AppendFormat("{0}\n", value);
        }

        public static void AppendEx(this StringBuilder sb, string name, string value)
        {
            sb.AppendFormat("{0}: {1}\n", name, value);
        }

    }
}
