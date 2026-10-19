using System.Security.Cryptography;
using System.Text;
using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Components.Shared;

internal static class ConfirmationDialog
{
    internal static class Css
    {
        internal const string Dialog = "lasagna-confirmation-dialog";
        internal const string Panel = "lasagna-confirmation-dialog__panel";
    }

    internal static string CreateId(string scope, string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var suffix = Convert.ToHexString(bytes)[..12].ToLowerInvariant();
        return $"lasagna-confirmation-{scope}-{suffix}";
    }

    internal static IHtmlContent Trigger(
        string dialogId,
        string label,
        params string[] classes)
        => FluentHtml.Button(button => button
            .Type("button")
            .Class(classes)
            .CommandFor(dialogId)
            .Command(Html.CommandType.show_modal)
            .Text(label));

    internal static IHtmlContent Render(
        string dialogId,
        string title,
        string message,
        IHtmlContent confirmation)
        => FluentHtml.Tag("dialog", dialog =>
        {
            var titleId = $"{dialogId}-title";

            dialog.Id(dialogId)
                .Class(Css.Dialog)
                .Aria("labelledby", titleId)
                .Div(panel =>
                {
                    panel.Class(Css.Panel, "p-4")
                        .H2(h2 => h2
                            .Id(titleId)
                            .Class("h4", "mb-3")
                            .Text(title))
                        .P(p => p
                            .Class("text-body-secondary", "mb-4")
                            .Text(message))
                        .Div(actions => actions
                            .Class("d-flex", "justify-content-end", "gap-2")
                            .Button(cancel => cancel
                                .Type("button")
                                .Class("btn", "btn-outline-secondary")
                                .CommandFor(dialogId)
                                .Command(Html.CommandType.close)
                                .Text("Cancel"))
                            .Add(confirmation));
                });
        });
}
