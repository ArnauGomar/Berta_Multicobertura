# sharpkml-wkt

Sharpkml saves the day when it comes to parsing KML files, but I needed these extensions in order to store the polygon and multigeometry objects in a SQL Server db.

Here's how to use it:

```csharp
//Load your kml just like normal
Parser parser = new Parser();
parser.ParseString(InputKml, false);

//Grab the placemark that has either a MultipleGeometry or Polygon Geomtery object
Placemark placemark = (Placemark)parser.Root;

//Extension doing what it does, giving us well known text ready for inserting into a SQL Geography column
string wkt = placemark.AsWKT();
```

