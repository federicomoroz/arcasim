using System.Xml.Linq;
using System.Xml.Schema;
using ArcaSim.Application.Contracts;

namespace ArcaSim.Tests.Support;

/// <summary>Checks an answer against the schemas of the WSDL ARCA publishes: what a client generated from it can deserialize.</summary>
internal static class Xsd
{
    /// <summary>The errors the schemas find in the element (a copy of it is validated, so the element keeps its place); none when it is valid.</summary>
    public static IReadOnlyList<string> Problems(XElement element, ServiceContract contract)
    {
        var problems = new List<string>();
        new XDocument(new XElement(element)).Validate(contract.Schemas, (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error) problems.Add(e.Message);
        });
        return problems;
    }

    /// <summary>Fails with the schema's complaints, and the answer after them, when the element is not valid for the contract.</summary>
    public static void AssertValid(XElement element, ServiceContract contract)
    {
        var problems = Problems(element, contract);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems) + Environment.NewLine + element);
    }
}
