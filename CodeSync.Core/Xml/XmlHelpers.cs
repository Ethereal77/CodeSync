using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace CodeSync.Core.Xml;

/// <summary>
///   Defines helpers for working with CodeSync XML documents.
/// </summary>
internal static class XmlHelpers
{
    /// <summary>
    ///   Loads the root XML element from an XML string and validates it
    ///   against the expected root name and schema version.
    /// </summary>
    /// <param name="xml">The XML string to parse.</param>
    /// <param name="expectedRoot">The expected root element name.</param>
    /// <param name="schemaVersion">The expected schema version.</param>
    /// <returns>The root XML element.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown if the <paramref name="xml"/> is <see langword="null"/>, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the XML is invalid or does not match the expected root and schema version.
    /// </exception>
    internal static XElement LoadRootFromXmlDocumentString(string xml, string expectedRoot, string schemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var root = XDocument.Parse(xml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo).Root
            ?? throw new InvalidDataException("The XML document has no root element.");

        if (root.Name.LocalName != expectedRoot ||
            (string?) root.Attribute("schemaVersion") != schemaVersion)
        {
            throw new InvalidDataException($"The document is not a CodeSync {expectedRoot} schema version {schemaVersion}.");
        }

        return root;
    }

    /// <summary>
    ///   Helper method to retrieve the required text content of an XML element.
    /// </summary>
    /// <param name="root">The XML element containing the required text.</param>
    /// <param name="name">The name of the child element whose text content is required.</param>
    /// <returns>The text content of the specified child element.</returns>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the specified child element is missing or its text content is <see langword="null"/>,
    ///   empty, or consists only of whitespace.
    /// </exception>
    internal static string RequiredText(XElement root, string name)
    {
        var value = (string?) root.Element(name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The XML document requires '{name}'.")
            : value;
    }

    /// <summary>
    ///   Helper method to retrieve the required attribute value of an XML element.
    /// </summary>
    /// <param name="element">The XML element containing the required attribute.</param>
    /// <param name="name">The name of the required attribute.</param>
    /// <returns>The value of the specified attribute.</returns>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the specified attribute is missing or its value is <see langword="null"/>,
    ///   empty, or consists only of whitespace.
    /// </exception>
    internal static string RequiredAttribute(XElement element, string name)
    {
        var value = (string?) element.Attribute(name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"The XML element '{element.Name.LocalName}' requires '{name}'.")
            : value;
    }

    /// <summary>
    ///   Retrieves the value of an optional XML attribute.
    /// </summary>
    /// <param name="element">The XML element containing the attribute.</param>
    /// <param name="name">The name of the attribute.</param>
    /// <returns>The value of the attribute, or <c>null</c> if it is not present or empty.</returns>
    internal static string? OptionalAttribute(XElement element, string name)
    {
        var value = (string?) element.Attribute(name);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    ///   Attempts to retrieve the required text content of an XML element,
    ///   adding any errors to the provided list.
    /// </summary>
    /// <param name="root">The XML element containing the required text.</param>
    /// <param name="name">The name of the child element whose text content is required.</param>
    /// <param name="errors">The list of errors to which any encountered errors will be added.</param>
    /// <returns>The text content of the specified child element, or <c>null</c> if an error occurred.</returns>
    internal static string? TryRequiredText(XElement root, string name, List<string> errors)
    {
        try
        {
            return RequiredText(root, name);
        }
        catch (InvalidDataException exception)
        {
            errors.Add(FormatElementError(root.Element(name) ?? root, exception.Message));
            return null;
        }
    }

    /// <summary>
    ///   Attempts to retrieve the required UTC timestamp from an XML element,
    ///   adding any errors to the provided list.
    /// </summary>
    /// <param name="root">The XML element containing the required timestamp.</param>
    /// <param name="name">The name of the child element whose timestamp is required.</param>
    /// <param name="errors">The list of errors to which any encountered errors will be added.</param>
    /// <returns>The UTC timestamp of the specified child element, or <c>null</c> if an error occurred.</returns>
    internal static DateTimeOffset? TryRequiredUtc(XElement root, string name, List<string> errors)
    {
        try
        {
            return RequiredUtc(root, name);
        }
        catch (InvalidDataException exception)
        {
            errors.Add(FormatElementError(root.Element(name) ?? root, exception.Message));
            return null;
        }
    }

    /// <summary>
    ///   Attempts to retrieve the required root directory path from an XML element,
    ///   adding any errors to the provided list.
    /// </summary>
    /// <param name="root">The XML element containing the required root directory path.</param>
    /// <param name="name">The name of the child element whose root directory path is required.</param>
    /// <param name="errors">The list of errors to which any encountered errors will be added.</param>
    /// <returns>The full path of the specified child element, or <c>null</c> if an error occurred.</returns>
    internal static string? TryRequiredRoot(XElement root, string name, List<string> errors)
    {
        var path = TryRequiredText(root, name, errors);
        if (path is null)
            return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            var message = FormatElementError(
                root.Element(name) ?? root,
                $"The XML element '{name}' is not a valid directory path: {exception.Message}");

            errors.Add(message);
            return null;
        }
    }

    /// <summary>
    ///   Attempts to parse an XML element using the provided parsing function,
    ///   adding any errors to the provided list.
    /// </summary>
    /// <typeparam name="T">The type of the parsed result.</typeparam>
    /// <param name="element">The XML element to parse.</param>
    /// <param name="errors">The list of errors to which any encountered errors will be added.</param>
    /// <param name="parse">The function that performs the parsing.</param>
    /// <returns>The parsed result, or <c>null</c> if an error occurred.</returns>
    internal static T? TryParseElement<T>(XElement element, List<string> errors, Func<T> parse)
        where T : class
    {
        try
        {
            return parse();
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or FormatException or OverflowException)
        {
            var message = FormatElementError(element, exception.Message);

            errors.Add(message);
            return null;
        }
    }

    /// <summary>
    ///   Formats an error message for an XML element, including line information if available.
    /// </summary>
    /// <param name="element">The XML element associated with the error.</param>
    /// <param name="message">The error message.</param>
    /// <returns>The formatted error message, including line information if available.</returns>
    internal static string FormatElementError(XElement element, string message)
    {
        var lineInfo = (IXmlLineInfo) element;

        return lineInfo.HasLineInfo()
            ? $"Line {lineInfo.LineNumber}: {message}"
            : message;
    }

    /// <summary>
    ///   Helper method to retrieve the required UTC timestamp from an XML element's text content.
    /// </summary>
    /// <param name="root">The XML element containing the required timestamp.</param>
    /// <param name="name">The name of the child element whose text content is required to be a UTC timestamp.</param>
    /// <returns>The UTC timestamp represented by the specified child element's text content.</returns>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the specified child element is missing, its text content is <see langword="null"/>,
    ///   empty, consists only of whitespace, or does not represent a valid UTC timestamp.
    /// </exception>
    internal static DateTimeOffset RequiredUtc(XElement root, string name)
    {
        var value = RequiredText(root, name);

        var valid = DateTimeOffset.TryParse(value,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.RoundtripKind,
                                            out var result);

        return !valid
            ? throw new InvalidDataException($"The XML element '{name}' must contain a UTC round-trip timestamp.")
            : result;
    }

    /// <summary>
    ///   Helper method to retrieve the required UTC timestamp from an XML element's attribute.
    /// </summary>
    /// <param name="element">The XML element containing the required attribute.</param>
    /// <param name="name">The name of the attribute whose value is required to be a UTC timestamp.</param>
    /// <returns>The UTC timestamp represented by the specified attribute's value.</returns>
    /// <exception cref="InvalidDataException">
    ///   Thrown if the specified attribute is missing, its value is <see langword="null"/>,
    ///   empty, consists only of whitespace, or does not represent a valid UTC timestamp.
    /// </exception>
    internal static DateTimeOffset RequiredUtcAttribute(XElement element, string name)
    {
        var value = RequiredAttribute(element, name);

        var valid = DateTimeOffset.TryParse(value,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.RoundtripKind,
                                            out var result);

        return !valid
            ? throw new InvalidDataException($"The XML attribute '{name}' must contain a UTC round-trip timestamp.")
            : result;
    }

    /// <summary>
    ///   Formats a DateTime value as an ISO 8601 round-trip timestamp.
    /// </summary>
    /// <param name="value">The DateTime value to format.</param>
    /// <returns>The formatted ISO 8601 round-trip timestamp.</returns>
    internal static string FormatTimestamp(DateTimeOffset value)
        => value.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    ///   Formats a DateTime value for display in the profile header.
    /// </summary>
    /// <param name="value">The DateTime value to format.</param>
    /// <returns>The formatted timestamp for the profile header.</returns>
    internal static string FormatHeaderTimestamp(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
