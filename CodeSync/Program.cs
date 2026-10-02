using CodeSync;
using CodeSync.Core;
using CodeSync.Core.Xml;

var workspace = new PhysicalWorkspace();

var xmlProfileStore = new XmlProfileStore();
var xmlConflictStore = new XmlConflictStore();
var xmlSkippedStore = new XmlSkippedStore();

var app = new App(workspace, xmlProfileStore, xmlConflictStore, xmlSkippedStore);

return await app.Run(args);
