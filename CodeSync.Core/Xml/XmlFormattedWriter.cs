using System.Text;

namespace CodeSync.Core.Xml;

/// <summary>
///   A helper class for writing formatted XML and generating a human-readable XML string.
/// </summary>
internal sealed class FormattedXmlWriter
{
    private readonly StringBuilder _builder = new();


    /// <summary>
    ///   Writes the XML declaration at the beginning of the document.
    /// </summary>
    public void WriteDeclaration()
    {
        _builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    }

    /// <summary>
    ///   Writes an XML comment with the specified text and indentation.
    /// </summary>
    /// <param name="text">The text of the comment.</param>
    /// <param name="indent">The number of spaces to indent the comment.</param>
    public void WriteComment(string text, int indent = 0)
    {
        var span = text.AsSpan();

        AppendIndentation(indent);
        _builder.AppendLine("<!--");

        foreach (var line in span.EnumerateLines())
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.IsEmpty)
            {
                _builder.AppendLine();
            }
            else
            {
                AppendIndentation(indent + 2);
                _builder.Append(trimmedLine).AppendLine();
            }
        }

        AppendIndentation(indent);
        _builder.AppendLine("-->");
    }

    /// <summary>
    ///   Writes the start tag of an XML element with the specified name, attributes, and indentation.
    /// </summary>
    /// <param name="name">The name of the element.</param>
    /// <param name="attributes">The attributes of the element.</param>
    /// <param name="indent">The number of spaces to indent the element.</param>
    public void WriteStartElement(string name,
                                  ReadOnlySpan<(string Name, string Value)> attributes,
                                  int indent = 0)
    {
        AppendIndentation(indent);

        if (attributes.IsEmpty)
        {
            _builder.AppendLine($"<{name}>");
        }
        else
        {
            _builder.Append($"<{name} ");

            bool first = true;

            foreach (var (attrName, attrValue) in attributes)
            {
                if (!first)
                    _builder.Append(' ');

                _builder.Append($"{attrName}=\"{EscapeAttribute(attrValue)}\"");

                first = false;
            }

            _builder.AppendLine(">");
        }
    }

    /// <summary>
    ///   Writes an empty XML element with the specified name, attributes, and indentation.
    /// </summary>
    /// <param name="name">The name of the element.</param>
    /// <param name="attributes">The attributes of the element.</param>
    /// <param name="indent">The number of spaces to indent the element.</param>
    public void WriteEmptyElement(string name,
                                  ReadOnlySpan<(string Name, string Value)> attributes,
                                  int indent = 0)
    {
        if (attributes.IsEmpty)
        {
            AppendIndentation(indent);
            _builder.AppendLine($"<{name} />");
            return;
        }

        if (attributes.Length == 1)
        {
            var (attrName, attrValue) = attributes[0];

            AppendIndentation(indent);
            _builder.AppendLine($"<{name} {attrName}=\"{EscapeAttribute(attrValue)}\" />");
            return;
        }

        var continuationIndent = indent + name.Length + 2;

        for (var index = 0; index < attributes.Length; index++)
        {
            var (attrName, attrValue) = attributes[index];

            if (index == 0)
            {
                AppendIndentation(indent);
                _builder.Append($"<{name} ");
            }
            else
            {
                AppendIndentation(continuationIndent);
            }

            _builder.Append($"{attrName}=\"{EscapeAttribute(attrValue)}\"");

            if (index < attributes.Length - 1)
            {
                _builder.AppendLine();
            }
        }

        _builder.AppendLine(" />");
    }

    /// <summary>
    ///   Writes an XML element with the specified name, value, and indentation.
    /// </summary>
    /// <param name="name">The name of the element.</param>
    /// <param name="value">The text content of the element.</param>
    /// <param name="indent">The number of spaces to indent the element.</param>
    public void WriteElement(string name, string value, int indent = 0)
    {
        AppendIndentation(indent);
        _builder.AppendLine($"<{name}>{EscapeText(value)}</{name}>");
    }

    /// <summary>
    ///   Writes the end tag of an XML element with the specified name and indentation.
    /// </summary>
    /// <param name="name">The name of the element.</param>
    /// <param name="indent">The number of spaces to indent the element.</param>
    public void WriteEndElement(string name, int indent = 0)
    {
        AppendIndentation(indent);
        _builder.AppendLine($"</{name}>");
    }

    /// <summary>
    ///   Writes indentation to the XML output based on the specified number of spaces.
    /// </summary>
    /// <param name="indent">The number of spaces to indent.</param>
    public void AppendIndentation(int indent = 0)
    {
        if (indent > 0)
        {
            _builder.Append(' ', repeatCount: indent);
        }
    }

    /// <summary>
    ///   Writes a blank line to the XML output.
    /// </summary>
    public void WriteBlankLine()
    {
        _builder.AppendLine();
    }

    /// <summary>
    ///   Returns the current XML content as a string.
    /// </summary>
    /// <returns>The current XML content as a string.</returns>
    public override string ToString()
    {
        return _builder.ToString();
    }

    /// <summary>
    ///   Escapes special characters in the text content of an XML element.
    /// </summary>
    /// <param name="value">The text content to escape.</param>
    /// <returns>The escaped text content.</returns>
    private static string EscapeText(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    /// <summary>
    ///   Escapes special characters in the value of an XML attribute.
    /// </summary>
    /// <param name="value">The attribute value to escape.</param>
    /// <returns>The escaped attribute value.</returns>
    private static string EscapeAttribute(string value)
    {
        return EscapeText(value)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
