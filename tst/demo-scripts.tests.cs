using System;
using Xunit;
using Xunit.Abstractions;
using DeckToSpadNext;

public class ExampleTest
{

    [Fact] 
    public void ProvideValue()
    {
        new DemoProvider().ProvideValue(null);
    }
}
