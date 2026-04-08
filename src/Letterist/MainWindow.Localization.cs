using Letterist.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Letterist;

public sealed partial class MainWindow
{
    private string L(string key) => UiLocalizationService.GetString(key);
    private string LF(string key, params object[] args) => UiLocalizationService.Format(key, args);

    private void UiLocalizationService_LanguageChanged(object? sender, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyLocalizedUiText();
            RefreshTranslationPanel();
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ApplyLocalizedUiText();
            RefreshTranslationPanel();
        });
    }

    private void ApplyLocalizedUiText()
    {
        UpdateWindowTitle();

        ApplyMenuBarLocalization();
        ApplyLayerMenuLocalization();
        ApplyPanelMenuLocalization();
        ApplyToolbarLocalization();
        ApplyContextToolbarLocalization();
        ApplyPanelLayoutToolbarLocalization();
        ApplySidebarTabLocalization();
        ApplySidebarContentLocalization();
        ApplyPropertiesPanelLocalization();
        ApplyTranslationTabLocalization();
        UpdateStatusBar();
    }

    private void ApplyMenuBarLocalization()
    {
        MenuFileItem.Title = L("menu.file");
        MenuFileNew.Text = L("menu.file.new");
        MenuFileOpen.Text = L("menu.file.open");
        RecentDocumentsMenu.Text = L("menu.file.open_recent");
        MenuFileSave.Text = L("menu.file.save");
        MenuFileSaveAs.Text = L("menu.file.save_as");
        MenuFileExport.Text = L("menu.file.export");
        MenuFileDocSettings.Text = L("menu.file.document_settings");
        MenuFileTranslate.Text = L("menu.file.translate");
        MenuFilePreferences.Text = L("menu.file.preferences");
        MenuFileStyleLibrary.Text = L("menu.file.style_library");
        MenuFileStyleImport.Text = L("menu.file.style_import");
        MenuFileStyleExport.Text = L("menu.file.style_export");
        MenuFileOpenImage.Text = L("menu.file.open_image");
        MenuFileImportDecoration.Text = L("menu.file.import_decoration");
        MenuFileBatchImport.Text = L("menu.file.batch_import");
        MenuFilePasteImage.Text = L("menu.file.paste_image");
        MenuFileClearBackground.Text = L("menu.file.clear_background");

        MenuEditItem.Title = L("menu.edit");
        MenuEditUndo.Text = L("menu.edit.undo");
        MenuEditRedo.Text = L("menu.edit.redo");
        MenuEditCut.Text = L("menu.edit.cut");
        MenuEditCopy.Text = L("menu.edit.copy");
        MenuEditPaste.Text = L("menu.edit.paste");
        MenuEditCopyStyle.Text = L("menu.edit.copy_style");
        MenuEditPasteStyle.Text = L("menu.edit.paste_style");
        MenuEditDuplicate.Text = L("menu.edit.duplicate");
        MenuEditSelectAll.Text = L("menu.edit.select_all");
        MenuEditFind.Text = L("menu.edit.find");
        MenuEditReplace.Text = L("menu.edit.replace");
        MenuEditAlign.Text = L("menu.edit.align");
        MenuEditDistribute.Text = L("menu.edit.distribute");
        MenuEditDelete.Text = L("menu.edit.delete");

        MenuAlignLeft.Text = L("align.left");
        MenuAlignCenter.Text = L("align.center");
        MenuAlignRight.Text = L("align.right");
        MenuAlignTop.Text = L("align.top");
        MenuAlignMiddle.Text = L("align.middle");
        MenuAlignBottom.Text = L("align.bottom");
        MenuDistributeH.Text = L("distribute.horizontally");
        MenuDistributeV.Text = L("distribute.vertically");

        MenuViewItem.Title = L("menu.view");
        MenuViewZoomFit.Text = L("menu.view.zoom_fit");
        MenuViewZoom100.Text = L("menu.view.zoom_100");
        MenuViewZoomIn.Text = L("menu.view.zoom_in");
        MenuViewZoomOut.Text = L("menu.view.zoom_out");
        MenuViewZoomSelection.Text = L("menu.view.zoom_selection");
        ShowGridMenuItem.Text = L("menu.view.show_grid");
        SnapToGridMenuItem.Text = L("menu.view.snap_to_grid");
        SnapToGuidesMenuItem.Text = L("menu.view.snap_to_guides");
        TypographyDiagnosticsMenuItem.Text = L("menu.view.typography_diagnostics");
        MenuHelpItem.Title = L("menu.help");
        MenuHelpAbout.Text = L("menu.help.about");

        MenuObjectsItem.Title = L("menu.objects");
        MenuObjectsAddBalloon.Text = L("menu.objects.add_balloon");
        MenuObjectsToggleTail.Text = L("menu.objects.toggle_tail");
        MenuObjectsLinkBalloons.Text = L("menu.objects.link_balloons");
        MenuObjectsUnlinkBalloons.Text = L("menu.objects.unlink_balloons");
        MenuObjectsGroup.Text = L("menu.objects.group");
        MenuObjectsUngroup.Text = L("menu.objects.ungroup");
        MenuObjectsAddImageFromUrl.Text = L("menu.objects.add_image_from_url");
    }

    private void ApplyLayerMenuLocalization()
    {
        MenuLayerItem.Title = L("menu.page");
        MenuPageAdd.Text = L("menu.page.add");
        MenuPageMoveUp.Text = L("menu.page.move_up");
        MenuPageMoveDown.Text = L("menu.page.move_down");
        MenuLayerNew.Text = L("menu.layer.new");
        MenuLayerMoveUp.Text = L("menu.layer.move_up_full");
        MenuLayerMoveDown.Text = L("menu.layer.move_down_full");
    }

    private void ApplyPanelMenuLocalization()
    {
        MenuPanelDesignMode.Text = L("menu.panel.design_mode");
        MenuPanelAdd.Text = L("menu.panel.add");
        MenuPanelTemplates.Text = L("menu.panel.templates_menu");
        MenuGuidesAddHorizontal.Text = L("menu.guides.add_horizontal");
        MenuGuidesAddVertical.Text = L("menu.guides.add_vertical");
    }

    private void ApplyToolbarLocalization()
    {
        ToolTipService.SetToolTip(SelectToolButton, L("toolbar.tooltip.select"));
        ToolTipService.SetToolTip(CreateBalloonButton, L("toolbar.tooltip.create_balloon"));
        ToolTipService.SetToolTip(ToggleTailButton, L("toolbar.tooltip.toggle_tail"));
        ToolTipService.SetToolTip(SaveToolbarButton, L("toolbar.tooltip.save"));
        ToolTipService.SetToolTip(ExportToolbarButton, L("toolbar.tooltip.export"));
        ToolTipService.SetToolTip(UndoButton, L("toolbar.tooltip.undo"));
        ToolTipService.SetToolTip(RedoButton, L("toolbar.tooltip.redo"));
        ToolTipService.SetToolTip(CutButton, L("toolbar.tooltip.cut"));
        ToolTipService.SetToolTip(CopyButton, L("toolbar.tooltip.copy"));
        ToolTipService.SetToolTip(PasteButton, L("toolbar.tooltip.paste"));
        ToolTipService.SetToolTip(DeleteToolbarButton, L("toolbar.tooltip.delete"));
        ToolTipService.SetToolTip(ZoomOutToolbarButton, L("toolbar.tooltip.zoom_out"));
        ToolTipService.SetToolTip(ZoomInToolbarButton, L("toolbar.tooltip.zoom_in"));
        ToolTipService.SetToolTip(ZoomFitToolbarButton, L("toolbar.tooltip.zoom_fit"));
        ToolTipService.SetToolTip(SnapToolbarToggle, L("toolbar.tooltip.snap"));

        if (BalloonTemplateQuickPaletteTitleText != null)
            BalloonTemplateQuickPaletteTitleText.Text = L("toolbar.recent_templates");
        if (BalloonTemplateQuickPaletteEmptyText != null)
            BalloonTemplateQuickPaletteEmptyText.Text = L("toolbar.recent_templates_empty");
        if (ToolbarBalloonTemplateEyedropperToggleButton != null)
        {
            ToolbarBalloonTemplateEyedropperToggleButton.Content = L("toolbar.style_pick");
            ToolTipService.SetToolTip(ToolbarBalloonTemplateEyedropperToggleButton, L("toolbar.tooltip.style_eyedropper"));
        }
    }

    private void ApplyContextToolbarLocalization()
    {
    }

    private void ApplyPanelLayoutToolbarLocalization()
    {
        PanelLayoutTitleText.Text = L("panel_layout.title");
        PanelToolHintText.Text = L("panel_layout.hint.select");
        ToolTipService.SetToolTip(PanelSelectToolButton, L("panel_layout.tooltip.select"));
        ToolTipService.SetToolTip(PanelRectToolButton, L("panel_layout.tooltip.draw_rect"));
        ToolTipService.SetToolTip(PanelRoundedRectToolButton, L("panel_layout.tooltip.draw_rounded"));
        ToolTipService.SetToolTip(PanelEllipseToolButton, L("panel_layout.tooltip.draw_ellipse"));
        ToolTipService.SetToolTip(PanelPolygonToolButton, L("panel_layout.tooltip.draw_polygon"));
        ToolTipService.SetToolTip(PanelFreeformToolButton, L("panel_layout.tooltip.draw_freeform"));
        PanelArrangeLabel.Text = L("panel_layout.arrange");
        PanelExitLabel.Text = L("panel_layout.exit");
    }

    private void ApplySidebarTabLocalization()
    {
        LeftSidebarPagesTabLabel.Text = L("sidebar.tab.pages");
        LeftSidebarLayersTabLabel.Text = L("sidebar.tab.layers");
        LeftSidebarPanelsTabLabel.Text = L("sidebar.tab.panels");
        ToolTipService.SetToolTip(LeftSidebarPagesTabButton, L("sidebar.tooltip.pages"));
        ToolTipService.SetToolTip(LeftSidebarLayersTabButton, L("sidebar.tooltip.layers"));
        ToolTipService.SetToolTip(LeftSidebarPanelsTabButton, L("sidebar.tooltip.panels"));
        LeftSidebarGuidesTabLabel.Text = L("sidebar.tab.guides");
        ToolTipService.SetToolTip(LeftSidebarGuidesTabButton, L("sidebar.tooltip.guides"));
        UpdateLeftSidebarTabHeaderMode();
    }

    private void ApplySidebarContentLocalization()
    {
        PanelTemplateSearchBox.PlaceholderText = L("template.search_placeholder");
        PanelTemplateCategoryFilterComboBox.Header = L("template.category_header");
        PanelTemplateLibraryEmptyText.Text = L("template.empty");

        if (PageDetailHeaderText != null) PageDetailHeaderText.Text = L("sidebar.header.page_detail");
        if (PageNameLabelText != null) PageNameLabelText.Text = L("tools.docsettings.name");
        if (PageNameTextBox != null) PageNameTextBox.PlaceholderText = L("tools.docsettings.name_placeholder");
        if (PageBackgroundImageLabel != null) PageBackgroundImageLabel.Text = L("page.section.background_image");
        if (BackgroundImageStatus != null) BackgroundImageStatus.Text = L("page.status.no_image");
        if (PageLoadImageButton != null) PageLoadImageButton.Content = L("page.button.load_image");
        if (PageClearImageButton != null) PageClearImageButton.Content = L("page.button.clear");
        if (PageBackgroundImageFitModeLabel != null) PageBackgroundImageFitModeLabel.Text = L("props.label.fit_mode");
        if (PageBackgroundImageFitModeComboBox != null) LocalizePageBackgroundImageFitCombo();
        if (PageCanvasSizeLabel != null) PageCanvasSizeLabel.Text = L("page.section.canvas_size");
        if (PageWidthBox != null) PageWidthBox.Header = L("page.header.width");
        if (PageHeightBox != null) PageHeightBox.Header = L("page.header.height");
        if (PageSizePresetComboBox != null) PageSizePresetComboBox.Header = L("page.header.preset");
        if (PageDpiLabel != null) PageDpiLabel.Text = L("page.section.dpi");
        if (PageDpiBox != null) PageDpiBox.Header = L("props.header.default_export_dpi");
        if (PageDpiHintText != null) PageDpiHintText.Text = L("page.hint.dpi");
        if (PageBackgroundColorLabel != null) PageBackgroundColorLabel.Text = L("page.section.background_color");
        if (PageBackgroundColorComboBox != null) LocalizeColorComboItems(PageBackgroundColorComboBox);
        if (PageReadingDirectionLabelText != null) PageReadingDirectionLabelText.Text = L("props.label.reading_direction");
        if (PageReadingDirectionComboBox != null)
        {
            SetComboItemContentByTag(PageReadingDirectionComboBox, "LeftToRight", L("guide.reading_dir.ltr"));
            SetComboItemContentByTag(PageReadingDirectionComboBox, "RightToLeft", L("guide.reading_dir.rtl"));
        }
    }

    private void ApplyPropertiesPanelLocalization()
    {
        BalloonDetailHeaderText.Text = L("props.header.balloon_detail");
        PanelDetailHeaderText.Text = L("props.header.panel_detail");
        LayerDetailHeaderText.Text = L("props.header.layer_detail");
        ImageDetailHeaderText.Text = L("props.header.image_detail");

        PropShapeTabLabel.Text = L("props.tab.shape");
        PropTextTabLabel.Text = L("props.tab.text");
        PropTailTabLabel.Text = L("props.tab.tail");
        PropEffectsTabLabel.Text = L("props.tab.effects");
        UpdatePropertiesTabHeaderMode();

        StyleStripComboBox.PlaceholderText = L("style_strip.placeholder");
        StyleStripEditButton.Content = L("common.edit");

        ApplyBalloonShapeTabLocalization();
        ApplyBalloonTextTabLocalization();
        ApplyBalloonFillTabLocalization();
        ApplyBalloonStylesTabLocalization();
        ApplyBalloonEffectsTabLocalization();
        ApplyBalloonTailTabLocalization();
        ApplyBalloonAdvancedTabLocalization();
        ApplyGuidesTabLocalization();
        ApplyPanelDetailLocalization();
        ApplyLayerDetailLocalization();
        ApplyImageDetailLocalization();
    }

    private void ApplyBalloonShapeTabLocalization()
    {
        LocalizeBalloonShapeCombo();
        BalloonShapeLabelText.Text = L("props.label.shape");
        CustomShapeLabelText.Text = L("props.label.custom_shape");
        ImportCustomShapeButton.Content = L("props.label.import_svg");
        CustomShapeStatusText.Text = L("custom_shape.not_loaded");

        BalloonPositionLabelText.Text = L("props.label.position");
        BalloonXBox.Header = L("props.header.x");
        BalloonYBox.Header = L("props.header.y");
        BalloonSizeLabelText.Text = L("props.label.size");
        BalloonWidthBox.Header = L("props.header.width");
        BalloonHeightBox.Header = L("props.header.height");
        BalloonRotationLabelText.Text = L("props.label.rotation");
        UpdateThoughtSliderLabel();

        BalloonPanelLabelText.Text = L("props.label.panel");
        BalloonConstrainToggle.Header = L("props.label.constrain_to_panel");
        BalloonConstrainToggle.OnContent = L("common.on");
        BalloonConstrainToggle.OffContent = L("common.off");

        FillColorLabelText.Text = L("props.label.fill_color");
        LocalizeColorComboItems(FillColorComboBox);
        StrokeColorLabelText.Text = L("props.label.stroke_color");
        LocalizeColorComboItems(StrokeColorComboBox);
        StrokeWidthLabelText.Text = L("props.label.stroke_width");

        BalloonOpacityLabelText.Text = L("props.label.opacity");
        CornerRadiusLabelText.Text = L("props.label.corner_radius");
        RotationLabelText2.Text = L("props.label.rotation");

        PaddingAdvancedLabelText.Text = L("props.label.padding_advanced");
        PaddingLeftLabelText.Text = L("props.label.left");
        PaddingRightLabelText.Text = L("props.label.right");
        PaddingTopLabelText.Text = L("props.label.top");
        PaddingBottomLabelText.Text = L("props.label.bottom");

        SizeLimitsLabelText.Text = L("props.label.size_limits");
        MinWidthLabelText.Text = L("props.label.min_w");
        MinHeightLabelText.Text = L("props.label.min_h");
        MaxWidthLabelText.Text = L("props.label.max_w");
        MaxHeightLabelText.Text = L("props.label.max_h");
        SizeLimitsHintText.Text = L("props.label.size_limits_hint");
    }

    private void ApplyBalloonTextTabLocalization()
    {
        PropTextSectionText.Text = L("props.section.text");
        BalloonTextEditBox.PlaceholderText = L("text.placeholder.balloon");

        PropFontSectionText.Text = L("props.section.font");
        FontFamilyLabelText.Text = L("props.label.family");
        FontSizeLabelText.Text = L("props.label.font_size");
        WeightStyleLabelText.Text = L("props.label.style");
        ColorCaseLabelText.Text = L("props.label.text_color");
        ScriptLabelText.Text = L("props.label.script");
        OutlineLabelText.Text = L("props.section.outline");

        LocalizeColorComboItems(TextColorComboBox);
        LocalizeColorComboItems(OutlineColorComboBox);
        LocalizeColorComboItems(Outline2ColorComboBox);
        LocalizeColorComboItems(Outline3ColorComboBox);

        LocalizeStrokePresetCombo(OutlineWidthPresetComboBox);
        OutlineWidthBox.Header = L("props.header.width");
        Outline2WidthBox.Header = L("props.header.stroke_2");
        Outline3WidthBox.Header = L("props.header.stroke_3");

        PropSpacingSectionText.Text = L("props.section.spacing");
        LineHeightLabelText.Text = L("props.label.line_height");
        FillHeightCheckBox.Content = L("props.label.spread_lines");
        LetterSpacingLabelText.Text = L("props.label.letter_spacing");
        VerticalOffsetLabelText.Text = L("props.label.vertical_offset");
        TextMarginLabelText.Text = L("props.label.text_margin");

        PropAlignmentSectionText.Text = L("props.section.alignment");
        AlignmentHorizontalLabelText.Text = L("props.label.horizontal");
        LocalizeTextAlignmentCombo();
        FitModeLabelText.Text = L("props.label.fit_mode");
        LocalizeFitModeCombo();

    }

    private void ApplyBalloonFillTabLocalization()
    {
        FillQuickAppearanceSectionText.Text = L("props.section.quick_appearance");

        BalloonTextColorLabel.Text = L("props.label.text_color");
        BalloonTextFillLabel.Text = L("props.label.fill_mode");
        BalloonFillSecondaryColorLabelText.Text = L("props.label.secondary_color");
        BalloonFillAngleLabelText.Text = L("props.label.gradient_angle");
        BalloonFillPatternLabelText.Text = L("fill.pattern");
        BalloonFillPatternScaleLabelText.Text = L("props.label.pattern_scale");
        BalloonFillImageSourceLabelText.Text = L("props.label.image_source");
        BalloonOutlineLabel.Text = L("props.section.outline");
        BalloonOutlineColorLabelText.Text = L("props.label.color");
        BalloonOutlineWidthLabelText.Text = L("props.header.width");
        BalloonOutlinePresetLabelText.Text = L("props.label.preset");
        BalloonOutlineToggle.Header = null;
        BalloonOutlineToggle.OnContent = L("common.on");
        BalloonOutlineToggle.OffContent = L("common.off");

        BalloonTextFillPatternComboBox.PlaceholderText = L("fill.pattern");
        BalloonTextFillImagePathBox.PlaceholderText = L("fill.no_image");
        BalloonTextFillImageBrowseButton.Content = L("common.browse");
        BalloonTextFillImageClearButton.Content = L("common.clear");

        BalloonOutlineWidthPresetComboBox.PlaceholderText = L("props.label.preset");

        LocalizeColorComboItems(BalloonTextColorComboBox);
        LocalizeColorComboItems(BalloonFillSecondaryColorComboBox);
        LocalizeColorComboItems(BalloonOutlineColorComboBox);

        SetComboItemContentByTag(BalloonTextFillTypeComboBox, "Solid", L("fill.solid"));
        SetComboItemContentByTag(BalloonTextFillTypeComboBox, "Linear", L("fill.linear_gradient"));
        SetComboItemContentByTag(BalloonTextFillTypeComboBox, "Radial", L("fill.radial_gradient"));
        SetComboItemContentByTag(BalloonTextFillTypeComboBox, "Pattern", L("fill.pattern"));
        SetComboItemContentByTag(BalloonTextFillTypeComboBox, "Image", L("fill.image"));

        SetComboItemContentByTag(BalloonTextFillPatternComboBox, "DiagonalStripes", L("fill.pattern.diagonal_stripes"));
        SetComboItemContentByTag(BalloonTextFillPatternComboBox, "Dots", L("fill.pattern.dots"));
        SetComboItemContentByTag(BalloonTextFillPatternComboBox, "Checkerboard", L("fill.pattern.checkerboard"));
        SetComboItemContentByTag(BalloonTextFillPatternComboBox, "Crosshatch", L("fill.pattern.crosshatch"));

        LocalizeStrokePresetCombo(BalloonOutlineWidthPresetComboBox);

        var fillType = _editorState?.Document?.SelectedBalloon?.TextStyle.FillType ?? TextFillType.Solid;
        UpdateFillModeUi(fillType);
    }

    private void ApplyBalloonAdvancedTabLocalization()
    {
        PropBalloonStateSectionText.Text = L("props.section.balloon_state");
        BalloonVisibilityToggle.Header = L("props.header.visible");
        BalloonVisibilityToggle.OnContent = L("common.yes");
        BalloonVisibilityToggle.OffContent = L("common.no");
        BalloonLockToggle.Header = L("props.header.locked");
        BalloonLockToggle.OnContent = L("common.yes");
        BalloonLockToggle.OffContent = L("common.no");

        TextOnPathCardLabelText.Text = L("props.label.text_on_path");
        TextOnPathEffectToggle.OnContent = L("common.on");
        TextOnPathEffectToggle.OffContent = L("common.off");
        TextOnPathOffsetBox.Header = L("props.header.offset");
        TextOnPathStartBox.Header = L("props.header.start_pct");
        TextOnPathEndBox.Header = L("props.header.end_pct");
        TextOnPathReverseToggle.Header = L("props.header.reverse_direction");
        TextOnPathReverseToggle.OnContent = L("text.reversed");
        TextOnPathReverseToggle.OffContent = L("text.normal");
        TextOnPathBezierLabelText.Text = L("props.label.bezier_points");
        TextOnPathStartXBox.Header = L("props.header.start_x");
        TextOnPathControl1XBox.Header = L("props.header.control1_x");
        TextOnPathControl2XBox.Header = L("props.header.control2_x");
        TextOnPathEndXBox.Header = L("props.header.end_x");
        TextOnPathStartYBox.Header = L("props.header.start_y");
        TextOnPathControl1YBox.Header = L("props.header.control1_y");
        TextOnPathControl2YBox.Header = L("props.header.control2_y");
        TextOnPathEndYBox.Header = L("props.header.end_y");

        TextWarpCardLabelText.Text = L("props.label.text_warp");
        TextWarpEffectToggle.OnContent = L("common.on");
        TextWarpEffectToggle.OffContent = L("common.off");
        TextWarpPresetLabelText.Text = L("props.label.preset");
        LocalizeWarpPresetCombo(TextWarpPresetComboBox);
        TextWarpIntensityBox.Header = L("props.header.intensity");
        TextWarpHorizontalBox.Header = L("props.header.horizontal");
        TextWarpVerticalBox.Header = L("props.header.vertical");
        TextWarpEnvelopeLabelText.Text = L("props.label.envelope_mesh");
        TextWarpTopLeftXBox.Header = L("props.header.top_left_x");
        TextWarpTopLeftYBox.Header = L("props.header.top_left_y");
        TextWarpBottomLeftXBox.Header = L("props.header.bottom_left_x");
        TextWarpBottomLeftYBox.Header = L("props.header.bottom_left_y");
        TextWarpTopRightXBox.Header = L("props.header.top_right_x");
        TextWarpTopRightYBox.Header = L("props.header.top_right_y");
        TextWarpBottomRightXBox.Header = L("props.header.bottom_right_x");
        TextWarpBottomRightYBox.Header = L("props.header.bottom_right_y");
        TextWarpResetMeshButton.Content = L("warp.reset_envelope");

        PropAdvancedSectionText.Text = L("props.section.overflow");
        OverflowLabelText.Text = L("props.label.overflow");
        LocalizeTextOverflowCombo();
    }

    private void ApplyBalloonStylesTabLocalization()
    {
        BalloonStyleLabelText.Text = L("props.label.balloon_style");
        ApplyBalloonStyleButton.Content = L("style.button.apply");
        UpdateBalloonStyleButton.Content = L("style.button.update");
        NewBalloonStyleButton.Content = L("style.button.new");
        RenameBalloonStyleButton.Content = L("style.button.rename");
        DeleteBalloonStyleButton.Content = L("style.button.delete");

        TextStyleLabelText.Text = L("props.label.text_style");
        ApplyTextStyleButton.Content = L("style.button.apply");
        UpdateTextStyleButton.Content = L("style.button.update");
        NewTextStyleButton.Content = L("style.button.new");
        RenameTextStyleButton.Content = L("style.button.rename");
        DeleteTextStyleButton.Content = L("style.button.delete");

        LastUsedTemplateLabelText.Text = L("balloon_template.label.last_used");
        NoLastUsedTemplateText.Text = L("balloon_template.label.no_last_used");
        BalloonTemplatesLabelText.Text = L("props.label.balloon_templates");
        TemplateActionsLabelText.Text = L("balloon_template.label.actions");
        BalloonTemplatePresetComboBox.PlaceholderText = L("balloon_template.placeholder.select");
        ApplyBalloonTemplateButton.Content = L("balloon_template.button.apply");
        UseBalloonTemplateForNewButton.Content = L("balloon_template.button.use_for_new");
        SaveBalloonTemplateButton.Content = L("balloon_template.button.save_from_selection");
        UpdateBalloonTemplateButton.Content = L("balloon_template.button.update_from_selection");
        RenameBalloonTemplateButton.Content = L("balloon_template.button.rename");
        DeleteBalloonTemplateButton.Content = L("balloon_template.button.delete");
        BalloonTemplateFavoriteToggleButton.Content = L("balloon_template.button.favorite");
        BalloonTemplateHotkeyComboBox.PlaceholderText = L("balloon_template.placeholder.hotkey");
        BalloonTemplateEyedropperToggleButton.Content = L("balloon_template.button.eyedropper");
        ToolTipService.SetToolTip(BalloonTemplateEyedropperToggleButton, L("balloon_template.eyedropper.tooltip_inactive"));
        ImportBalloonTemplatePackButton.Content = L("balloon_template.button.import_pack");
        ExportBalloonTemplatePackButton.Content = L("balloon_template.button.export_pack");
        QuickApplyHintText.Text = L("props.label.quick_apply_hint");
        SetComboItemContentByTag(BalloonTemplateHotkeyComboBox, "none", L("balloon_template.hotkey.none"));
    }

    private void ApplyBalloonEffectsTabLocalization()
    {
        BalloonBodyEffectsSectionText.Text = $"{L("props.label.balloon_style").ToUpperInvariant()} {L("props.tab.effects").ToUpperInvariant()}";
        PropTextEffectsSectionText.Text = $"{L("props.label.text_style").ToUpperInvariant()} {L("props.tab.effects").ToUpperInvariant()}";
        BalloonTextShadowsLabel.Text = L("props.header.shadow");

        ShadowCardLabelText.Text = L("props.header.shadow");
        GlowCardLabelText.Text = L("props.header.glow_effect");
        GradientCardLabelText.Text = L("props.header.gradient_fill");

        ShadowToggle.Header = null;
        ShadowToggle.OnContent = L("common.on");
        ShadowToggle.OffContent = L("common.off");

        GlowToggle.Header = null;
        GlowToggle.OnContent = L("common.on");
        GlowToggle.OffContent = L("common.off");

        GradientToggle.Header = null;
        GradientToggle.OnContent = L("common.on");
        GradientToggle.OffContent = L("common.off");
        PatternToggle.Header = null;
        PatternToggle.OnContent = L("common.on");
        PatternToggle.OffContent = L("common.off");

        ShadowColorLabelText.Text = L("props.label.color");
        ShadowOpacityLabelText.Text = L("props.header.opacity_pct");
        ShadowFalloffLabelText.Text = L("props.header.falloff");
        ShadowOffsetXLabelText.Text = L("props.header.offset_x");
        ShadowOffsetYLabelText.Text = L("props.header.offset_y");
        GlowColorLabelText.Text = L("props.label.color");
        GlowRadiusLabelText.Text = L("props.header.size");
        GlowIntensityLabelText.Text = L("props.header.intensity");
        GradientTypeLabelText.Text = L("props.label.gradient_type");
        GradientAngleLabelText.Text = L("props.label.gradient_angle");
        GradientStartLabelText.Text = L("common.start");
        GradientEndLabelText.Text = L("common.end");
        PatternCardLabelText.Text = L("fill.pattern");
        PatternTypeLabelText.Text = L("fill.pattern");
        PatternScaleLabelText.Text = L("props.label.scale");
        PatternAngleLabelText.Text = L("props.label.rotation");
        PatternSecondaryColorLabelText.Text = L("props.label.secondary_color");
        BalloonPatternImageSourceLabelText.Text = L("props.label.image_source");
        BalloonPatternImagePathBox.PlaceholderText = L("fill.no_image");
        BalloonPatternImageBrowseButton.Content = L("common.browse");
        BalloonPatternImageClearButton.Content = L("common.clear");

        LocalizeColorComboItems(ShadowColorComboBox);
        LocalizeColorComboItems(GlowColorComboBox);
        SetComboItemContentByTag(GradientTypeComboBox, "Linear", L("fill.linear_gradient"));
        SetComboItemContentByTag(GradientTypeComboBox, "Radial", L("fill.radial_gradient"));
        LocalizeColorComboItems(GradientStartColorComboBox);
        LocalizeColorComboItems(GradientEndColorComboBox);
        SetComboItemContentByTag(PatternTypeComboBox, "DiagonalStripes", L("fill.pattern.diagonal_stripes"));
        SetComboItemContentByTag(PatternTypeComboBox, "Dots", L("fill.pattern.dots"));
        SetComboItemContentByTag(PatternTypeComboBox, "Checkerboard", L("fill.pattern.checkerboard"));
        SetComboItemContentByTag(PatternTypeComboBox, "Crosshatch", L("fill.pattern.crosshatch"));
        LocalizeColorComboItems(PatternSecondaryColorComboBox);

        BalloonTextShadowToggle.Header = null;
        BalloonTextShadowToggle.OnContent = L("common.on");
        BalloonTextShadowToggle.OffContent = L("common.off");
        BalloonTextShadow1OffsetXLabelText.Text = L("props.header.offset_x");
        BalloonTextShadow1OffsetYLabelText.Text = L("props.header.offset_y");
        BalloonTextShadow1BlurLabelText.Text = L("props.header.blur");
        BalloonTextShadow1OpacityLabelText.Text = L("props.header.opacity_pct");
        LocalizeColorComboItems(BalloonTextShadow1ColorComboBox);

        BalloonTextOuterGlowLabelText.Text = L("props.label.outer_glow");
        BalloonTextOuterGlowToggle.Header = null;
        BalloonTextOuterGlowToggle.OnContent = L("common.on");
        BalloonTextOuterGlowToggle.OffContent = L("common.off");
        BalloonTextOuterGlowSizeLabelText.Text = L("props.header.size");
        BalloonTextOuterGlowOpacityLabelText.Text = L("props.header.opacity_pct");
        LocalizeColorComboItems(BalloonTextOuterGlowColorComboBox);

        BalloonTextInnerGlowLabelText.Text = L("props.label.inner_glow");
        BalloonTextInnerGlowToggle.Header = null;
        BalloonTextInnerGlowToggle.OnContent = L("common.on");
        BalloonTextInnerGlowToggle.OffContent = L("common.off");
        BalloonTextInnerGlowSizeLabelText.Text = L("props.header.size");
        BalloonTextInnerGlowOpacityLabelText.Text = L("props.header.opacity_pct");
        LocalizeColorComboItems(BalloonTextInnerGlowColorComboBox);

        BalloonTextExtrusionLabelText.Text = L("props.label.3d_extrusion");
        BalloonTextExtrusionToggle.Header = null;
        BalloonTextExtrusionToggle.OnContent = L("common.on");
        BalloonTextExtrusionToggle.OffContent = L("common.off");
        BalloonTextExtrusionDepthLabelText.Text = L("props.header.depth");
        BalloonTextExtrusionOpacityLabelText.Text = L("props.header.opacity_pct");
        BalloonTextExtrusionAngleLabelText.Text = L("props.header.extrusion_angle");
        LocalizeColorComboItems(BalloonTextExtrusionColorComboBox);

        BalloonTextMotionBlurLabelText.Text = L("props.label.motion_blur");
        BalloonTextMotionBlurToggle.Header = null;
        BalloonTextMotionBlurToggle.OnContent = L("common.on");
        BalloonTextMotionBlurToggle.OffContent = L("common.off");
        BalloonTextMotionBlurDistanceLabelText.Text = L("props.header.distance");
        BalloonTextMotionBlurAngleLabelText.Text = L("props.header.blur_angle");
        BalloonTextMotionBlurOpacityLabelText.Text = L("props.header.opacity_pct");
    }

    private void ApplyBalloonTailTabLocalization()
    {
        HasTailToggle.Header = L("props.header.has_tail");
        HasTailToggle.OnContent = L("common.yes");
        HasTailToggle.OffContent = L("common.no");

        TailStyleLabelText.Text = L("props.label.tail_style");
        LocalizeTailStyleCombo(TailStyleComboBox);
        TailWidthLabelText.Text = L("props.label.tail_width");
        TailCurvatureLabelText.Text = L("props.label.curvature");
        TailCurveCenterLabelText.Text = L("props.label.curve_center");
        TailInsetLabelText.Text = L("props.label.tail_inset");

        ResetTailAttachmentButton.Content = L("tail.button.reset_attachment");
        AddTailButton.Content = L("tail.button.add_another");

        TailLinksHeaderText.Text = L("props.section.balloon_links");
        LinkColorLabelText.Text = L("props.label.link_color");
        LocalizeColorComboItems(LinkColorComboBox);
        LinkBorderWidthLabelText.Text = L("props.label.border_width");
        ConnectorWidthLabelText.Text = L("props.label.connector_width");
        DashStyleLabelText.Text = L("props.label.dash_style");
        LocalizeLinkDashCombo();
        LinkedBalloonsLabelText.Text = L("props.label.linked_balloons");
        ClearLinksButton.Content = L("common.clear");
        LinkListEmptyText.Text = L("props.label.no_links");
    }

    private void ApplyBalloonLinksTabLocalization() { }

    private void ApplyGuidesTabLocalization()
    {
        SnapToGuidesToggle.Header = L("props.header.snap_to_guides");
        SnapToGuidesToggle.OnContent = L("guide.snap_on");
        SnapToGuidesToggle.OffContent = L("guide.snap_off");
        LockGuidesToggle.Header = L("props.header.lock_guides");
        LockGuidesToggle.OnContent = L("guide.locked");
        LockGuidesToggle.OffContent = L("guide.unlocked");

        PanelBoundariesLabelText.Text = L("props.label.panel_boundaries");
        LocalizePanelBoundaryCombo();
        BoundaryTipText.Text = L("props.hint.boundary_tip");

        PanelSafeMarginsLabelText.Text = L("props.label.panel_safe_margins");
        PanelSafeGuidesToggle.Header = L("props.header.show_safe_area");
        PanelSafeGuidesToggle.OnContent = L("common.on");
        PanelSafeGuidesToggle.OffContent = L("common.off");
        SafeGuidesTipText.Text = L("props.hint.safe_guides");

        PanelGuttersLabelText.Text = L("props.label.panel_gutters");
        PanelGutterGuidesToggle.Header = L("props.header.show_gutter");
        PanelGutterGuidesToggle.OnContent = L("common.on");
        PanelGutterGuidesToggle.OffContent = L("common.off");
        GutterTipText.Text = L("props.hint.gutter_tip");

        GutterStyleLabelText.Text = L("props.label.gutter_style");
        PanelGutterFillToggle.Header = L("props.header.fill_gutter");
        PanelGutterFillToggle.OnContent = L("common.on");
        PanelGutterFillToggle.OffContent = L("common.off");
        LocalizeColorComboItems(PanelGutterColorComboBox);
        PanelBorderStyleLabelText.Text = L("props.label.panel_border_style");
        LocalizeLineStyleCombo(PanelGutterStrokeStyleComboBox);

        ReadingDirectionLabelText.Text = L("props.label.reading_direction");
        LocalizeReadingDirectionCombo();
        ReadingArrowsTipText.Text = L("props.hint.reading_arrows");

        AddGuideLabelText.Text = L("props.label.add_guide");
        AddHorizontalGuideButton.Content = L("guide.horizontal");
        AddVerticalGuideButton.Content = L("guide.vertical");

        PageGuidesLabelText.Text = L("props.label.page_guides");
        ClearGuidesButton.Content = L("guide.clear_all");
        GuideListEmptyText.Text = L("guide.empty_none");
    }

    private void ApplyPanelDetailLocalization()
    {
        PanelNameLabelText.Text = L("props.label.name");
        PanelNameTextBox.PlaceholderText = L("panel.placeholder.name");
        PanelShapeLabelText.Text = L("props.label.shape");
        LocalizePanelShapeCombo();
        PanelCornerRadiusLabelText.Text = L("props.label.corner_radius");
        PanelBorderLabelText.Text = L("props.label.border");
        LocalizeColorComboItems(PanelBorderColorComboBox);
        PanelBorderWidthLabelText.Text = L("props.label.width");
        PanelBorderStyleDetailLabelText.Text = L("props.label.style");
        LocalizeLineStyleCombo(PanelBorderStyleComboBox);

        PanelPositionLabelText.Text = L("props.label.position");
        PanelXBox.Header = L("props.header.x");
        PanelYBox.Header = L("props.header.y");
        PanelSizeLabelText.Text = L("props.label.size");
        PanelWidthBox.Header = L("props.header.width");
        PanelHeightBox.Header = L("props.header.height");

        PanelAspectRatioLockToggle.Header = L("props.header.lock_aspect_ratio");
        PanelAspectRatioLockToggle.OnContent = L("common.yes");
        PanelAspectRatioLockToggle.OffContent = L("common.no");

        PanelSizePresetLabelText.Text = L("props.label.size_preset");
        LocalizePanelSizePresetCombo();

        PanelSafeMarginLabelText.Text = L("props.label.safe_margin");
        PanelSafeMarginBox.Header = L("props.header.inset");

        GutterOverridesLabelText.Text = L("props.label.gutter_overrides");
        PanelCustomGutterToggle.Header = L("props.header.custom_gutters");
        PanelCustomGutterToggle.OnContent = L("common.on");
        PanelCustomGutterToggle.OffContent = L("common.off");
        PanelGutterLeftBox.Header = L("props.header.left");
        PanelGutterRightBox.Header = L("props.header.right");
        PanelGutterTopBox.Header = L("props.header.top");
        PanelGutterBottomBox.Header = L("props.header.bottom");

        PanelBleedLabelText.Text = L("props.label.bleed");
        PanelBleedLeftBox.Header = L("props.header.left");
        PanelBleedRightBox.Header = L("props.header.right");
        PanelBleedTopBox.Header = L("props.header.top");
        PanelBleedBottomBox.Header = L("props.header.bottom");

        ReadingOrderLabelText.Text = L("props.label.reading_order");

        PanelImageLabelText.Text = L("props.label.panel_image");
        LoadPanelImageButton.Content = L("panel.image.load_replace");
        ClearPanelImageButton.Content = L("panel.image.clear");
        PanelImageStatusText.Text = L("panel.image.no_image");
        PanelImageFitModeLabelText.Text = L("props.label.fit_mode");
        LocalizePanelImageFitCombo();
        PanelImageOpacityLabelText.Text = L("props.label.image_opacity");
        PanelImageLockToggle.Header = L("props.header.lock_position");
        PanelImageLockToggle.OnContent = L("common.yes");
        PanelImageLockToggle.OffContent = L("common.no");
        PanelImageExportToggle.Header = L("props.header.visible_in_export");
        PanelImageExportToggle.OnContent = L("common.yes");
        PanelImageExportToggle.OffContent = L("common.no");
        ResetPanelImageButton.Content = L("panel.image.reset_position");

        PanelTemplatesLabelText.Text = L("props.label.templates");
        PanelTemplateComboBox.PlaceholderText = L("panel.layout.apply_template");
        PanelTemplateApplyButtonText.Text = L("props.button.apply_template");
        PanelTemplateMergeButtonText.Text = L("props.button.merge_template");
        PanelTemplateRenameButtonText.Text = L("props.button.edit_template");
        PanelTemplateDeleteButtonText.Text = L("props.button.delete_template");
        PanelTemplateImportButtonText.Text = L("props.button.import_templates");
        PanelTemplateExportButtonText.Text = L("props.button.export_template");

        ToolTipService.SetToolTip(PanelTemplateApplyButton, L("template.tooltip.apply"));
        ToolTipService.SetToolTip(PanelTemplateMergeButton, L("template.tooltip.merge"));
        ToolTipService.SetToolTip(PanelTemplateRenameButton, L("template.tooltip.edit"));
        ToolTipService.SetToolTip(PanelTemplateDeleteButton, L("template.tooltip.delete"));
        ToolTipService.SetToolTip(PanelTemplateImportButton, L("template.tooltip.import"));
        ToolTipService.SetToolTip(PanelTemplateExportButton, L("template.tooltip.export"));
    }

    private void ApplyLayerDetailLocalization()
    {
        LayerNameLabelText.Text = L("props.label.name");
        LayerNameTextBox.PlaceholderText = L("layer.placeholder.name");
        LayerVisibilityToggle.Header = L("props.header.visible");
        LayerVisibilityToggle.OnContent = L("common.yes");
        LayerVisibilityToggle.OffContent = L("common.no");
        LayerLockToggle.Header = L("props.header.locked");
        LayerLockToggle.OnContent = L("common.yes");
        LayerLockToggle.OffContent = L("common.no");
        LayerOpacityLabelText.Text = L("props.label.opacity");
        LayerBlendModeLabelText.Text = L("props.label.blend_mode");
        LayerInfoLabelText.Text = L("props.label.info");
    }

    private void ApplyImageDetailLocalization()
    {
        FloatingImageBasicTabLabelText.Text = L("props.tab.basic");
        FloatingImageAdvancedTabLabelText.Text = L("props.tab.advanced");
        ImageFileLabelText.Text = L("props.label.image_file");
        FloatingImageSourceLabelText.Text = L("props.label.image_source");
        FloatingImageMediaInfoLabelText.Text = L("props.label.info");
        OwnershipLabelText.Text = L("props.label.ownership");
        FloatingImagePanelLabelText.Text = L("props.label.panel");
        FloatingImagePanelComboBox.PlaceholderText = L("common.none");
        FloatingImageConstrainToggle.Header = L("props.label.constrain_to_panel");
        FloatingImageConstrainToggle.OnContent = L("common.on");
        FloatingImageConstrainToggle.OffContent = L("common.off");
        FloatingImagePositionLabelText.Text = L("props.label.position");
        FloatingImageXBox.Header = L("props.header.x");
        FloatingImageYBox.Header = L("props.header.y");
        FloatingImageSizeLabelText.Text = L("props.label.size");
        FloatingImageWidthBox.Header = L("props.header.width");
        FloatingImageHeightBox.Header = L("props.header.height");
        FloatingImageRotationLabelText.Text = L("props.label.rotation");
        FloatingImageShadowHeaderText.Text = L("props.header.shadow");
        FloatingImageShadowToggle.OnContent = L("common.on");
        FloatingImageShadowToggle.OffContent = L("common.off");
        FloatingImageShadowColorLabelText.Text = L("props.label.color");
        FloatingImageShadowOpacityLabelText.Text = $"{L("props.label.opacity")} %";
        FloatingImageShadowFalloffLabelText.Text = L("props.header.falloff");
        FloatingImageShadowOffsetXLabelText.Text = L("props.header.offset_x");
        FloatingImageShadowOffsetYLabelText.Text = L("props.header.offset_y");
        FloatingImageGlowHeaderText.Text = L("props.header.glow_effect");
        FloatingImageGlowToggle.OnContent = L("common.on");
        FloatingImageGlowToggle.OffContent = L("common.off");
        FloatingImageGlowColorLabelText.Text = L("props.label.color");
        FloatingImageGlowSizeLabelText.Text = L("props.label.size");
        FloatingImageGlowIntensityLabelText.Text = L("props.header.intensity");
        FloatingImageOpacityLabelText.Text = L("props.label.opacity");
        FloatingImageVisibilityToggle.Header = L("props.header.visible");
        FloatingImageVisibilityToggle.OnContent = L("common.yes");
        FloatingImageVisibilityToggle.OffContent = L("common.no");
        FloatingImageLockToggle.Header = L("props.header.locked");
        FloatingImageLockToggle.OnContent = L("common.yes");
        FloatingImageLockToggle.OffContent = L("common.no");
    }

    private void LocalizeTextAlignmentCombo()
    {
        SetComboItemContentByTag(TextAlignmentComboBox, "Left", L("align.left"));
        SetComboItemContentByTag(TextAlignmentComboBox, "Center", L("align.center"));
        SetComboItemContentByTag(TextAlignmentComboBox, "Right", L("align.right"));
    }

    private void LocalizeFitModeCombo()
    {
        SetComboItemContentByTag(FitModeComboBox, "GrowBalloon", L("text.fit.auto_size"));
        SetComboItemContentByTag(FitModeComboBox, "None", L("text.fit.manual"));
        SetComboItemContentByTag(FitModeComboBox, "ShrinkToFit", L("text.fit.shrink_to_fit"));
    }

    private void LocalizeTextOverflowCombo()
    {
        SetComboItemContentByTag(TextOverflowComboBox, "Warn", L("text.overflow.warn"));
        SetComboItemContentByTag(TextOverflowComboBox, "Clip", L("text.overflow.clip"));
        SetComboItemContentByTag(TextOverflowComboBox, "Allow", L("text.overflow.allow"));
    }

    private void LocalizeWarpPresetCombo(ComboBox combo)
    {
        SetComboItemContentByTag(combo, "None", L("warp.none"));
        SetComboItemContentByTag(combo, "ArcUp", L("warp.arc_up"));
        SetComboItemContentByTag(combo, "ArcDown", L("warp.arc_down"));
        SetComboItemContentByTag(combo, "Bulge", L("warp.bulge"));
        SetComboItemContentByTag(combo, "Pinch", L("warp.pinch"));
        SetComboItemContentByTag(combo, "Wave", L("warp.wave"));
        SetComboItemContentByTag(combo, "Flag", L("warp.flag"));
    }

    private void LocalizeTailStyleCombo(ComboBox combo)
    {
        SetComboItemContentByTag(combo, "Pointer", L("tail.style.pointer"));
        SetComboItemContentByTag(combo, "Curved", L("tail.style.curved"));
        SetComboItemContentByTag(combo, "ThoughtBubbles", L("tail.style.thought_bubbles"));
        SetComboItemContentByTag(combo, "Squiggly", L("tail.style.squiggly"));
    }

    private void LocalizeLinkDashCombo()
    {
        SetComboItemContentByTag(LinkDashComboBox, "Solid", L("line_style.solid"));
        SetComboItemContentByTag(LinkDashComboBox, "Dash", L("line_style.dash"));
        SetComboItemContentByTag(LinkDashComboBox, "Dot", L("line_style.dot"));
        SetComboItemContentByTag(LinkDashComboBox, "DashDot", L("line_style.dash_dot"));
    }

    private void LocalizeLineStyleCombo(ComboBox combo)
    {
        SetComboItemContentByTag(combo, "None", L("line_style.none"));
        SetComboItemContentByTag(combo, "Solid", L("line_style.solid"));
        SetComboItemContentByTag(combo, "Dashed", L("line_style.dashed"));
        SetComboItemContentByTag(combo, "Dotted", L("line_style.dotted"));
        SetComboItemContentByTag(combo, "DashDot", L("line_style.dash_dot"));
    }

    private void LocalizePanelBoundaryCombo()
    {
        SetComboItemContentByTag(PanelBoundaryVisibilityComboBox, "Always", L("guide.panel_boundary.always"));
        SetComboItemContentByTag(PanelBoundaryVisibilityComboBox, "LayoutOnly", L("guide.panel_boundary.layout_only"));
        SetComboItemContentByTag(PanelBoundaryVisibilityComboBox, "Hover", L("guide.panel_boundary.hover"));
        SetComboItemContentByTag(PanelBoundaryVisibilityComboBox, "Hidden", L("guide.panel_boundary.hidden"));
    }

    private void LocalizeReadingDirectionCombo()
    {
        SetComboItemContentByTag(ReadingDirectionComboBox, "LeftToRight", L("guide.reading_dir.ltr"));
        SetComboItemContentByTag(ReadingDirectionComboBox, "RightToLeft", L("guide.reading_dir.rtl"));
        SetComboItemContentByTag(ReadingDirectionComboBox, "Manual", L("guide.reading_dir.manual"));
    }

    private void LocalizePanelShapeCombo()
    {
        SetComboItemContentByTag(PanelShapeComboBox, "Rectangle", L("shape.rectangle"));
        SetComboItemContentByTag(PanelShapeComboBox, "RoundedRect", L("shape.rounded_rect"));
        SetComboItemContentByTag(PanelShapeComboBox, "Ellipse", L("shape.ellipse"));
        SetComboItemContentByTag(PanelShapeComboBox, "Custom", L("shape.custom"));
    }

    private void LocalizePanelSizePresetCombo()
    {
        SetComboItemContentByTag(PanelSizePresetComboBox, "custom", L("panel.aspect.custom"));
        SetComboItemContentByTag(PanelSizePresetComboBox, "1:1", L("panel.aspect.square"));
        SetComboItemContentByTag(PanelSizePresetComboBox, "1.618:1", L("panel.aspect.golden_ratio"));
        SetComboItemContentByTag(PanelSizePresetComboBox, "custom-ratio", L("panel.aspect.custom_ratio"));
    }

    private void LocalizePanelImageFitCombo()
    {
        SetComboItemContentByTag(PanelImageFitModeComboBox, "Fill", L("panel.image.fit.fill"));
        SetComboItemContentByTag(PanelImageFitModeComboBox, "Fit", L("panel.image.fit.fit"));
        SetComboItemContentByTag(PanelImageFitModeComboBox, "Stretch", L("panel.image.fit.stretch"));
        SetComboItemContentByTag(PanelImageFitModeComboBox, "Original", L("panel.image.fit.original"));
    }

    private void LocalizePageBackgroundImageFitCombo()
    {
        if (PageBackgroundImageFitModeComboBox == null) return;

        SetComboItemContentByTag(PageBackgroundImageFitModeComboBox, "Fill", L("panel.image.fit.fill"));
        SetComboItemContentByTag(PageBackgroundImageFitModeComboBox, "Fit", L("panel.image.fit.fit"));
        SetComboItemContentByTag(PageBackgroundImageFitModeComboBox, "Stretch", L("panel.image.fit.stretch"));
    }

    private void LocalizeStrokePresetCombo(ComboBox combo)
    {
        SetComboItemContentByTag(combo, "0", L("stroke.preset_none"));
        SetComboItemContentByTag(combo, "1", L("stroke.preset_thin"));
        SetComboItemContentByTag(combo, "2", L("stroke.preset_medium"));
        if (combo == OutlineWidthPresetComboBox)
        {
            SetComboItemContentByTag(combo, "3", L("stroke.preset_thick"));
            SetComboItemContentByTag(combo, "4", L("stroke.preset_heavy"));
        }
        else
        {
            SetComboItemContentByTag(combo, "4", L("stroke.preset_thick"));
            SetComboItemContentByTag(combo, "6", L("stroke.preset_thick"));
            SetComboItemContentByTag(combo, "8", L("stroke.preset_heavy"));
        }
    }

    private void LocalizePageSizePresets()
    {
        if (PageSizePresetComboBox == null) return;
        SetComboItemContentByIndex(PageSizePresetComboBox, 0, L("page.preset.custom"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 1, L("page.preset.print_header"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 2, L("page.preset.us_letter"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 3, L("page.preset.a4"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 4, L("page.preset.us_comic"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 5, L("page.preset.manga_b5"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 6, L("page.preset.web_header"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 7, L("page.preset.full_hd"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 8, L("page.preset.hd"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 9, L("page.preset.instagram"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 10, L("page.preset.social_media"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 11, L("page.preset.square"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 12, L("page.preset.strip_header"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 13, L("page.preset.3_panel"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 14, L("page.preset.4_panel"));
        SetComboItemContentByIndex(PageSizePresetComboBox, 15, L("page.preset.webtoon"));
    }

    private void LocalizeBalloonShapeCombo()
    {
        if (ShapeComboBox == null) return;
        SetComboItemContentByTag(ShapeComboBox, "Oval", L("shape.oval"));
        SetComboItemContentByTag(ShapeComboBox, "RoundedRect", L("shape.rounded_rect"));
        SetComboItemContentByTag(ShapeComboBox, "Rectangle", L("shape.rectangle"));
        SetComboItemContentByTag(ShapeComboBox, "Radio", L("shape.radio"));
        SetComboItemContentByTag(ShapeComboBox, "Thought", L("shape.thought"));
        SetComboItemContentByTag(ShapeComboBox, "Splat", L("shape.splat"));
        SetComboItemContentByTag(ShapeComboBox, "Burst", L("shape.burst"));
        SetComboItemContentByTag(ShapeComboBox, "Whisper", L("shape.whisper"));
        SetComboItemContentByTag(ShapeComboBox, "Custom", L("shape.custom_svg"));
    }

    private void LocalizeColorComboItems(ComboBox combo)
    {
        foreach (var item in combo.Items)
        {
            if (item is not ComboBoxItem cbi || cbi.Tag is not string tag) continue;

            var localized = tag switch
            {
                "#FFFFFF" or "#ffffff" => L("color.white"),
                "#000000" => L("color.black"),
                "#F0F0F0" or "#EEEEEE" or "#AAAAAA" or "#DDDDDD" => L("color.light_gray"),
                "#CCCCCC" or "#888888" or "#666666" => L("color.gray"),
                "#444444" or "#2C2C2C" or "#333333" or "#1E1E1E" => L("color.dark_gray"),
                "#FF0000" or "#CC0000" or "#C62828" => L("color.red"),
                "#0066FF" or "#0066CC" or "#0078D7" or "#1565C0" => L("color.blue"),
                "#00CC00" or "#008800" => L("color.green"),
                "#FFFF00" or "#FFCC00" or "#F9A825" => L("color.yellow"),
                "#FF8800" or "#FF6600" or "#FFE0C0" => L("color.orange"),
                "#9933CC" or "#6633CC" => L("color.purple"),
                "#FF66CC" => L("color.pink"),
                "#663300" => L("color.brown"),
                "#FFFEF5" => L("color.off_white"),
                "#E8F4FF" or "#E0F0FF" or "#C0E0FF" => L("color.light_blue"),
                "#FFFAEB" => L("color.cream"),
                "Transparent" => L("color.transparent"),
                "#FFFFC0" or "#FFFFA0" => L("color.light_yellow"),
                "#FFE0E0" or "#FFC0D0" => L("color.light_red"),
                "#E0FFE0" or "#C0FFD0" => L("color.light_green"),
                "CUSTOM" or "custom" => L("color.custom"),
                _ => null
            };

            if (localized != null) cbi.Content = localized;
        }
    }

    private void ApplyTranslationTabLocalization()
    {
        if (TranslationTabContent == null)
        {
            return;
        }

        TranslationWorkflowTitleText.Text = L("translation.workflow.title");
        TranslationTargetLanguagesTitleText.Text = L("translation.target_languages.title");
        TranslationNewLanguageBox.PlaceholderText = L("translation.add_language.placeholder");
        TranslationAddLanguageButton.Content = L("translation.add_language.button");
        TranslationNoLanguagesText.Text = L("translation.no_languages");
        TranslationActiveLanguageComboBox.Header = L("translation.active_language.header");
        TranslationSearchBox.PlaceholderText = L("translation.search.placeholder");

        TranslationResultsTitleText.Text = L("translation.results.title");

        TranslationSelectedBalloonTitleText.Text = L("translation.selected_balloon.title");
        TranslationTargetTextBox.Header = L("translation.target_translation.header");
        TranslationClearButton.Content = L("translation.action.clear");
        TranslationSaveButton.Content = L("translation.action.save");
        ExportTranslationsButtonText.Text = L("translation.button.export_strings");
        ImportTranslationsButtonText.Text = L("translation.button.import_strings");
    }

    private static void SetToggleButtonText(ToggleButton button, string text)
    {
        if (button.Content is TextBlock textBlock)
        {
            textBlock.Text = text;
            return;
        }

        button.Content = text;
    }

    private static void SetComboItemContentByTag(ComboBox comboBox, string tag, string content)
    {
        foreach (var item in comboBox.Items)
        {
            if (item is not ComboBoxItem comboItem || comboItem.Tag is not string itemTag)
            {
                continue;
            }

            if (string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase))
            {
                comboItem.Content = content;
                return;
            }
        }
    }

    private static void SetComboItemContentByIndex(ComboBox comboBox, int index, string content)
    {
        if (index >= 0 && index < comboBox.Items.Count && comboBox.Items[index] is ComboBoxItem cbi)
        {
            cbi.Content = content;
        }
    }
}
