using Letterist.Commands;
using Letterist.Model;
using System.Linq;
using Xunit;

namespace Letterist.Tests.Commands;

public class PageTemplateCommandTests
{
    [Fact]
    public void CreatePageTemplateCommand_CapturesPageSetup()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;

        var createLayer = new CreateLayerCommand("Notes");
        createLayer.Execute(doc);

        var createGroup = new CreateLayerGroupCommand(page.Id, "Group 1");
        createGroup.Execute(doc);

        var addToGroup = new AddLayerToGroupCommand(page.Id, createLayer.CreatedLayerId, createGroup.CreatedGroupId);
        addToGroup.Execute(doc);

        var createGuide = new CreateGuideCommand(page.Id, GuideOrientation.Horizontal, 120);
        createGuide.Execute(doc);

        var cmd = new CreatePageTemplateCommand(page.Id, "Template A");
        cmd.Execute(doc);

        var template = doc.PageTemplates.FirstOrDefault(t => t.Id == cmd.CreatedTemplateId);
        Assert.NotNull(template);
        Assert.Equal(page.Size, template!.Size);
        Assert.Equal(page.Guides.Count, template.Guides.Count);
        Assert.Equal(page.LayerGroups.Count, template.LayerGroups.Count);

        var templateLayer = template.Layers.First(l => l.Name == "Notes");
        Assert.Equal(createGroup.CreatedGroupId, templateLayer.GroupId);
    }

    [Fact]
    public void CreatePageFromTemplateCommand_CreatesPageWithTemplateStructure()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;

        var createLayer = new CreateLayerCommand("Notes");
        createLayer.Execute(doc);

        var createGroup = new CreateLayerGroupCommand(page.Id, "Group 1");
        createGroup.Execute(doc);

        var addToGroup = new AddLayerToGroupCommand(page.Id, createLayer.CreatedLayerId, createGroup.CreatedGroupId);
        addToGroup.Execute(doc);

        var createGuide = new CreateGuideCommand(page.Id, GuideOrientation.Vertical, 200);
        createGuide.Execute(doc);

        var templateCmd = new CreatePageTemplateCommand(page.Id, "Template A");
        templateCmd.Execute(doc);

        var template = doc.PageTemplates.First(t => t.Id == templateCmd.CreatedTemplateId);

        var pageCmd = new CreatePageFromTemplateCommand(template.Id, "Page 2", insertIndex: doc.Pages.Count, setActive: true);
        pageCmd.Execute(doc);

        var newPage = doc.FindPage(pageCmd.CreatedPageId);
        Assert.NotNull(newPage);
        Assert.Equal(template.Size, newPage!.Size);
        Assert.Equal(template.Guides.Count, newPage.Guides.Count);
        Assert.Equal(template.LayerGroups.Count, newPage.LayerGroups.Count);
        Assert.Equal(template.Layers.Count, newPage.Layers.Count);

        var templateLayerIds = template.Layers.Select(l => l.Id).ToHashSet();
        Assert.DoesNotContain(newPage.Layers, layer => templateLayerIds.Contains(layer.Id));

        var templateActiveLayerName = template.Layers.First(l => l.Id == template.ActiveLayerId).Name;
        Assert.Equal(templateActiveLayerName, newPage.FindLayer(newPage.ActiveLayerId)!.Name);

        var notesLayer = newPage.Layers.First(l => l.Name == "Notes");
        var group = newPage.LayerGroups.First(g => g.Name == "Group 1");
        Assert.Equal(group.Id, notesLayer.GroupId);
    }

    [Fact]
    public void RenameAndDeletePageTemplateCommands_WorkAsExpected()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;

        var createTemplate = new CreatePageTemplateCommand(page.Id, "Template A");
        createTemplate.Execute(doc);

        var templateId = createTemplate.CreatedTemplateId;
        var rename = new RenamePageTemplateCommand(templateId, "Template B");
        rename.Execute(doc);

        Assert.Equal("Template B", doc.FindPageTemplate(templateId)!.Name);

        var delete = new DeletePageTemplateCommand(templateId);
        delete.Execute(doc);

        Assert.Null(doc.FindPageTemplate(templateId));

        delete.Undo(doc);
        Assert.NotNull(doc.FindPageTemplate(templateId));

        rename.Undo(doc);
        Assert.Equal("Template A", doc.FindPageTemplate(templateId)!.Name);
    }
}
