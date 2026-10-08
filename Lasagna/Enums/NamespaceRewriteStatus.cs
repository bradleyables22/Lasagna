using System;
using System.Collections.Generic;
using System.Text;

namespace Lasagna.Enums
{
	internal enum NamespaceRewriteStatus
	{
		Unchanged,
		Rewritten,
		NotApplicable,
		Unsupported,
		ParseError
	}
}
