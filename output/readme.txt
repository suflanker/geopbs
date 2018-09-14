PortableBasemapServer is an free(LGPL)/easy to use WPF application, which wraps various kinds of data source to provide identical REST basemap service, which can be used by ArcGIS API or OpenLayers.

Any comments or suggestions are welcomed to post on: http://blog.newnaw.com/?p=890
==============================================================================

Updates:
v3.0
Add support for running PBS as a Windows service.
Add support for OGC WMTS specification. Any service published by PBS now is a WMTS service.
Add support for OGC WMS service data source. Input a dynamic WMS service to use it as a tiled map service.
Add support for GaoDe file cache data source.
Add some local logs.
Bug fix.

v2.0.7
Add support for local caching of RasterImage data source.
Add support for converting/downloading by Shapefile boundaries when convert/download Online Maps data source to local MBTiles format.
Add support for comapcting the .mbtiles file when converting ArcGIS Cache/Online Maps data source to local MBTiles(.mbtiles) format.

v2.0.6
Add support for converting ArcGIS Cache/Online Maps data source to local MBTiles(.mbtiles) format.
Add support for local caching of online maps data source.
Improve the performance of reading ArcGIS Tile Package(.tpk) file format.
Bug fix.

v2.0.5
Add additional parameter settings(layer, layerDefs, etc.) for PBS service, which data source type is ArcGISDynamicMapService.
Add support for REST admin api, changeParams operation for ArcGISDynamicMapService data source, to dynamically change additional parameters.
Add support for ArcGISTiledMapService data source.
Add support for customizing online maps by modifying CustomOnlineMaps.xml file.
Add support for automatically save/load last time configuration.
Add context menu for system tray icon.
Bug fix.

v2.0.4
Add support for REST admin api, enable/disable operation, to enable/disable memory cache ability.
Add support for REST admin api, clearByService operation, to clear PBS service memory cache by service name and port.
Add support for Chinese language UI.
Add support for minimize to system tray.
Bug fix.

v2.0.3
Add support for REST admin api, addService/deleteService operation.
Add support for outputing old/embossed visual style tile image.

v2.0.2
Add support for data source type of ArcGIS Tile Package format.
Add support for clearing memory cache of a single PBS service(previously, clear memory cache of all services).

v2.0.1
Add support for controlling memory cache of a single PBS service.
Add support for outputing count of tiles from dynamically generated and from memory cache.

v2.0
Add support for memory cache, which caching generated tiles in RAM memory, so the next time another client could retrieve the tile directly from memory rather than generating on the fly. This feature could tremendously improve performance of PBS and so it can serves more concurrent clients. Memory cache feature of PBS is completed by using Memcached, which is a free and open source, high-performance, distributed memory object caching system(http://memcached.org/).

v1.0.6
Add support for outputing styled tile images, including grayscale and invert visual style. i.e., Gray style map could make users stay focus on your thematic content.http://blogs.esri.com/Support/blogs/arcgisonline/archive/2011/09/29/arcgis-online-canvas-maps-now-available.aspx

v1.0.5
Add support for outputing ArcGIS Server endpoint json response of PBS services, so that PBS services could be used in various ArcGIS application, such as Silverlight Viewer.

v1.0.4
Add support for save/load services as configuration files. Configurations arc stored in a sqlited database.
Add support of directly selecting your IP address.

v1.0.3:
Add support for .sid raster format.
Add support for ArcGIS Server Image Service. Until ArcGIS Server 10, cache mechanism is still unavailable. Using PBS, you can provide an Image Service the DynamicTile capability, as if the Image Service is cached.
Add log info of senconds per tile.

v1.0.2
Add support for .ecw raster format.
Add support for .vrt raster format. Now you can use .vrt file as Raster Catalog in ArcGIS to provide several raster files as inputs without actually merge them.

v1.0.1
Add support for Raster data source. You can choose most raster image as data source to provide dynamic tile map service effect. Raster data source is supported by using GDAL.