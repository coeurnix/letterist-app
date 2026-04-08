$ErrorActionPreference = "Stop"

$langs = @("en","es","fr","pt-br","de","zh-cn","ja","ko")
$all = @{}
foreach ($lang in $langs) {
    $path = Join-Path $PSScriptRoot "..\src\Letterist\Localization\strings.$lang.json"
    $obj = Get-Content $path -Raw | ConvertFrom-Json
    $map = @{}
    foreach ($p in $obj.PSObject.Properties) {
        $map[$p.Name] = [string]$p.Value
    }
    $all[$lang] = $map
}

$prefixes = @(
    "menu.","toolbar.tooltip.","sidebar.tooltip.","context.tooltip.","panel_layout.tooltip.","ctx.",
    "props.tab.","props.header.","props.button.","props.label.","props.section.",
    "shape.","tail.","text.fit.","text.overflow.","text.button.","warp.","fill.","line_style.","guide.","arrange.","align.","distribute.",
    "prefs.field.","tools.docsettings.","tools.unit.","tools.color.","tools.dialog.",
    "export.dialog.","font_chooser.","template.","templates.dialog.","translation.tooltip.",
    "find.dialog.","find.scope.","find.option.","find.window.","replace.dialog.","replace.option.","replace.window.",
    "delete.dialog.","panel_layout.dialog.","input.dialog."
)

$keys = $all["en"].Keys |
    Where-Object {
        $k = $_
        $prefixes | Where-Object { $k.StartsWith($_) }
    } |
    Sort-Object -Unique

$labelsByLang = [ordered]@{}
foreach ($lang in $langs) {
    $labels = [ordered]@{}
    foreach ($k in $keys) {
        $v = $all[$lang][$k]
        if ([string]::IsNullOrWhiteSpace($v)) { $v = $all["en"][$k] }
        if ([string]::IsNullOrWhiteSpace($v)) { $v = $k }
        $labels[$k] = $v
    }
    $labelsByLang[$lang] = $labels
}

$data = [ordered]@{
    keys = $keys
    labels = $labelsByLang
}

$jsonData = $data | ConvertTo-Json -Depth 8 -Compress
$outPath = Join-Path $PSScriptRoot "..\src\Letterist\Help\contents\index.html"

$html = @'
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Letterist Help</title>
<style>
:root{--bg:#0f1623;--panel:#1a2539;--line:#385076;--text:#eef3ff;--muted:#c7d2e7;--accent:#85b8ff;--ok:#89deb5}
*{box-sizing:border-box}
body{margin:0;padding:22px;background:radial-gradient(1000px 430px at 100% -120px,#2b457466 0%,#0f162300 60%),var(--bg);color:var(--text);font:14px/1.5 "Segoe UI",Arial,sans-serif}
main{max-width:1200px;margin:0 auto}
h1{margin:0;font-size:2rem}h2{margin:0;font-size:1.3rem}
p,li,td,th{color:var(--muted)}
.panel{margin-top:14px;padding:14px;border:1px solid var(--line);border-radius:10px;background:var(--panel)}
.note{border-left:4px solid var(--ok);background:#89deb520;color:#dcf6ea;padding:10px 12px;border-radius:6px}
.controls{display:flex;gap:8px;flex-wrap:wrap}
input[type="search"]{background:#111a2b;color:#edf3ff;border:1px solid var(--line);border-radius:8px;padding:8px 10px;min-width:320px;max-width:560px;width:100%}
button{background:#30466f;color:#eef3ff;border:1px solid #4f73ac;border-radius:8px;padding:8px 12px;cursor:pointer}
button:hover{filter:brightness(1.05)}
details{margin-top:10px;border:1px solid var(--line);border-radius:8px;background:#202d45}
summary{cursor:pointer;padding:9px 11px;color:#eaf0ff;font-weight:600}
.count{color:#a9bbdc;font-weight:400;margin-left:8px}
.table-wrap{padding:0 10px 10px;overflow:auto}
table{width:100%;border-collapse:collapse;min-width:760px}
th,td{border:1px solid var(--line);padding:6px 8px;vertical-align:top}
th{background:#283857;color:#f3f7ff;font-weight:600}
code{font-family:Consolas,"Courier New",monospace;background:#132036;border:1px solid #35507a;border-radius:4px;padding:1px 5px;color:#f5f8ff}
a{color:var(--accent);text-decoration:none}a:hover{text-decoration:underline}
.foot{margin-top:18px;padding-top:10px;border-top:1px solid var(--line);color:#9db2d7}
@media(max-width:900px){body{padding:14px}input[type="search"]{min-width:0}}
</style>
</head>
<body>
<main>
<h1 id="title"></h1>
<p id="lead"></p>
<p class="note" id="note"></p>

<section class="panel">
<h2 id="flowTitle"></h2>
<ol id="flowList"></ol>
</section>

<section class="panel">
<h2 id="refTitle"></h2>
<p id="refLead"></p>
<div class="controls">
<input id="search" type="search" />
<button id="expandAll" type="button"></button>
<button id="collapseAll" type="button"></button>
</div>
<div id="groups"></div>
</section>

<section class="panel">
<p id="quickText"></p>
<a id="quickLink" href="../quickstart/index.html"></a>
</section>

<p class="foot" id="foot"></p>
</main>

<script>
const HELP_DATA = __DATA__;

(function(){
    function normalizeLanguage(v){
        if(!v)return "en";
        const l=v.toLowerCase();
        if(l.startsWith("zh")) return "zh-cn";
        if(l.startsWith("pt")) return "pt-br";
        if(l.startsWith("es")||l.startsWith("fr")||l.startsWith("de")||l.startsWith("ja")||l.startsWith("ko")) return l.split("-")[0];
        return "en";
    }

    const UI = {
        "en":{
            title:"Letterist Complete Help and Control Reference",
            lead:"Exhaustive, searchable control reference for the full interactive app surface.",
            note:"Search by control label or by resource key. Use this page for both production training and automation QA mapping.",
            flowTitle:"Recommended Production Flow",
            flow:["Set document/page standards first (size, DPI, defaults).","Build panel structure and reading order before dense dialogue work.","Load artwork and lock critical elements (background, panel image, floating image).","Letter balloons, tune text fit and styling, then shape tails/links.","Run translation pass and QA pass before export.","Export with final format/scope/language packaging settings."],
            refTitle:"Complete Control Index",
            refLead:"Includes menus, toolbars, context menus, property controls, style options, and dialog/settings controls.",
            search:"Search controls by label or key...",
            expand:"Expand all",
            collapse:"Collapse all",
            colControl:"Control",
            colKey:"Resource Key",
            colBehavior:"Behavior",
            quickText:"Need a procedural checklist instead of full reference?",
            quickLink:"Open Quickstart",
            foot:"Tip: Use resource keys to align help entries with test automation assertions.",
            groups:{menu:"Menus",toolbar:"Toolbar and Quick Actions",ctx:"Context Menus",props:"Property Panel Controls",options:"Shape, Style, and Option Values",settings:"Dialogs, Preferences, and Settings",other:"Other Controls"},
            desc:{dialog:"Dialog prompt or confirmation control.",history:"History control (undo/redo/repeat).",search:"Search/filter/find-replace control.",save:"Save/export output control.",remove:"Remove/clear/unlink action.",toggle:"Mode/state toggle control.",create:"Create/add/apply action.",open:"Open/load/select/import action.",navigate:"Navigation/zoom/selection control.",adjust:"Adjusts numeric/style/layout parameter.",reference:"Related workflow control."}
        },
        "es":{
            title:"Referencia completa de ayuda y controles de Letterist",
            lead:"Referencia exhaustiva y buscable de toda la superficie interactiva de la aplicación.",
            note:"Busca por etiqueta de control o por clave de recurso. Úsala para formación de producción y mapeo QA de automatización.",
            flowTitle:"Flujo de producción recomendado",
            flow:["Define primero estándares de documento/página (tamaño, DPI, predeterminados).","Construye paneles y orden de lectura antes del diálogo denso.","Carga ilustración y bloquea elementos críticos (fondo, imagen de panel, imagen flotante).","Rotula globos, ajusta texto y estilos, luego colas/enlaces.","Ejecuta traducción y QA antes de exportar.","Exporta con formato/alcance/idioma finales."],
            refTitle:"Índice completo de controles",
            refLead:"Incluye menús, barras, menús contextuales, controles de propiedades, opciones de estilo y controles de diálogo/configuración.",
            search:"Buscar controles por etiqueta o clave...",
            expand:"Expandir todo",
            collapse:"Contraer todo",
            colControl:"Control",
            colKey:"Clave de recurso",
            colBehavior:"Comportamiento",
            quickText:"¿Necesitas una lista procedural en lugar de la referencia completa?",
            quickLink:"Abrir inicio rápido",
            foot:"Consejo: usa las claves de recurso para alinear ayuda y automatización de pruebas.",
            groups:{menu:"Menús",toolbar:"Barra y acciones rápidas",ctx:"Menús contextuales",props:"Controles del panel de propiedades",options:"Valores de forma, estilo y opciones",settings:"Diálogos, preferencias y configuración",other:"Otros controles"},
            desc:{dialog:"Control de diálogo, aviso o confirmación.",history:"Control de historial (deshacer/rehacer/repetir).",search:"Control de búsqueda/filtro/reemplazo.",save:"Control de guardado/exportación.",remove:"Acción de eliminar/limpiar/desvincular.",toggle:"Control de activación de modo/estado.",create:"Acción de crear/agregar/aplicar.",open:"Acción de abrir/cargar/seleccionar/importar.",navigate:"Control de navegación/zoom/selección.",adjust:"Ajusta parámetro numérico/estilo/diseño.",reference:"Control relacionado con el flujo."}
        },
        "fr":{
            title:"Référence complète d'aide et de contrôles Letterist",
            lead:"Référence exhaustive et recherchable de toute la surface interactive de l'application.",
            note:"Recherchez par libellé de contrôle ou clé de ressource. Utile pour la formation production et le mapping QA automatisé.",
            flowTitle:"Flux de production recommandé",
            flow:["Définir d'abord les standards document/page (taille, DPI, défauts).","Construire les cases et l'ordre de lecture avant les dialogues denses.","Charger l'illustration et verrouiller les éléments critiques (fond, image de case, image flottante).","Lettrer les bulles, ajuster le texte/style, puis queues/liens.","Lancer traduction et QA avant export.","Exporter avec format/portée/langue finaux."],
            refTitle:"Index complet des contrôles",
            refLead:"Inclut menus, barres, menus contextuels, contrôles de propriétés, options de style et contrôles de dialogue/paramètres.",
            search:"Rechercher des contrôles par libellé ou clé...",
            expand:"Tout développer",
            collapse:"Tout réduire",
            colControl:"Contrôle",
            colKey:"Clé de ressource",
            colBehavior:"Comportement",
            quickText:"Besoin d'une checklist procédurale plutôt qu'une référence complète ?",
            quickLink:"Ouvrir le démarrage rapide",
            foot:"Astuce : utilisez les clés de ressource pour aligner aide et automatisation de tests.",
            groups:{menu:"Menus",toolbar:"Barre et actions rapides",ctx:"Menus contextuels",props:"Contrôles du panneau de propriétés",options:"Valeurs de forme, style et options",settings:"Dialogues, préférences et paramètres",other:"Autres contrôles"},
            desc:{dialog:"Contrôle de dialogue, invite ou confirmation.",history:"Contrôle d'historique (annuler/rétablir/répéter).",search:"Contrôle de recherche/filtre/remplacement.",save:"Contrôle d'enregistrement/export.",remove:"Action de suppression/effacement/dissociation.",toggle:"Contrôle de bascule mode/état.",create:"Action de création/ajout/application.",open:"Action d'ouverture/chargement/sélection/import.",navigate:"Contrôle de navigation/zoom/sélection.",adjust:"Ajuste un paramètre numérique/style/mise en page.",reference:"Contrôle lié au flux."}
        },
        "pt-br":{
            title:"Referência completa de ajuda e controles do Letterist",
            lead:"Referência exaustiva e pesquisável de toda a superfície interativa do aplicativo.",
            note:"Pesquise por rótulo do controle ou chave de recurso. Útil para treinamento de produção e mapeamento de QA de automação.",
            flowTitle:"Fluxo de produção recomendado",
            flow:["Defina primeiro padrões de documento/página (tamanho, DPI, padrões).","Monte quadros e ordem de leitura antes de diálogos densos.","Carregue arte e trave elementos críticos (fundo, imagem de quadro, imagem flutuante).","Letre balões, ajuste texto/estilo e depois caudas/links.","Execute tradução e QA antes de exportar.","Exporte com formato/escopo/idioma finais."],
            refTitle:"Índice completo de controles",
            refLead:"Inclui menus, barras, menus de contexto, controles de propriedades, opções de estilo e controles de diálogo/configuração.",
            search:"Pesquisar controles por rótulo ou chave...",
            expand:"Expandir tudo",
            collapse:"Recolher tudo",
            colControl:"Controle",
            colKey:"Chave de recurso",
            colBehavior:"Comportamento",
            quickText:"Precisa de um checklist procedural em vez da referência completa?",
            quickLink:"Abrir início rápido",
            foot:"Dica: use chaves de recurso para alinhar ajuda e automação de testes.",
            groups:{menu:"Menus",toolbar:"Barra e ações rápidas",ctx:"Menus de contexto",props:"Controles do painel de propriedades",options:"Valores de forma, estilo e opções",settings:"Diálogos, preferências e configurações",other:"Outros controles"},
            desc:{dialog:"Controle de diálogo, prompt ou confirmação.",history:"Controle de histórico (desfazer/refazer/repetir).",search:"Controle de busca/filtro/substituição.",save:"Controle de salvar/exportar.",remove:"Ação de remover/limpar/desvincular.",toggle:"Controle de alternância de modo/estado.",create:"Ação de criar/adicionar/aplicar.",open:"Ação de abrir/carregar/selecionar/importar.",navigate:"Controle de navegação/zoom/seleção.",adjust:"Ajusta parâmetro numérico/estilo/layout.",reference:"Controle relacionado ao fluxo."}
        },
        "de":{
            title:"Vollständige Letterist-Hilfe und Steuerungsreferenz",
            lead:"Umfassende, durchsuchbare Referenz der gesamten interaktiven Anwendungsoberfläche.",
            note:"Suchen Sie nach Steuerungslabel oder Ressourcenschlüssel. Für Produktionstraining und QA-Automatisierungs-Mapping.",
            flowTitle:"Empfohlener Produktionsablauf",
            flow:["Zuerst Dokument-/Seitenstandards festlegen (Größe, DPI, Defaults).","Panels und Lesereihenfolge vor dichtem Dialog aufbauen.","Artwork laden und kritische Elemente sperren (Hintergrund, Panelbild, schwebendes Bild).","Sprechblasen lettern, Text/Stil anpassen, dann Schwänze/Links.","Übersetzungs- und QA-Pass vor Export ausführen.","Mit finalen Format/Umfang/Sprachoptionen exportieren."],
            refTitle:"Vollständiger Steuerungsindex",
            refLead:"Enthält Menüs, Symbolleisten, Kontextmenüs, Eigenschaftensteuerungen, Stiloptionen und Dialog-/Einstellungselemente.",
            search:"Steuerungen nach Label oder Schlüssel suchen...",
            expand:"Alle aufklappen",
            collapse:"Alle einklappen",
            colControl:"Steuerung",
            colKey:"Ressourcenschlüssel",
            colBehavior:"Verhalten",
            quickText:"Benötigen Sie statt der Vollreferenz eine prozedurale Checkliste?",
            quickLink:"Quickstart öffnen",
            foot:"Tipp: Ressourcenschlüssel helfen beim Abgleich zwischen Hilfe und Testautomatisierung.",
            groups:{menu:"Menüs",toolbar:"Symbolleiste und Schnellaktionen",ctx:"Kontextmenüs",props:"Eigenschaften-Panelsteuerungen",options:"Form-, Stil- und Optionswerte",settings:"Dialoge, Präferenzen und Einstellungen",other:"Weitere Steuerungen"},
            desc:{dialog:"Dialog-, Eingabe- oder Bestätigungssteuerung.",history:"Verlaufssteuerung (Rückgängig/Wiederholen).",search:"Such-/Filter-/Ersetzen-Steuerung.",save:"Speicher-/Exportsteuerung.",remove:"Entfernen/Löschen/Trennen-Aktion.",toggle:"Modus-/Status-Umschalter.",create:"Erstellen/Hinzufügen/Anwenden-Aktion.",open:"Öffnen/Laden/Importieren-Aktion.",navigate:"Navigation/Zoom/Auswahl-Steuerung.",adjust:"Passt numerischen Stil-/Layoutparameter an.",reference:"Workflow-bezogene Steuerung."}
        },
        "zh-cn":{
            title:"Letterist 完整帮助与控件参考",
            lead:"覆盖应用全部交互面的完整可搜索参考。",
            note:"可按控件标签或资源键搜索。用于生产培训与自动化 QA 映射。",
            flowTitle:"推荐生产流程",
            flow:["先设定文档/页面标准（尺寸、DPI、默认值）。","先完成分镜结构和阅读顺序，再处理密集对白。","加载图像并锁定关键元素（背景、分镜图、浮动图）。","完成气泡与文字样式，再调整尾巴/链接。","导出前先跑翻译与 QA 流程。","按最终格式/范围/语言打包导出。"],
            refTitle:"完整控件索引",
            refLead:"包含菜单、工具栏、右键菜单、属性控件、样式选项和对话框/设置控件。",
            search:"按标签或资源键搜索控件...",
            expand:"全部展开",
            collapse:"全部折叠",
            colControl:"控件",
            colKey:"资源键",
            colBehavior:"行为",
            quickText:"需要流程清单而不是完整参考？",
            quickLink:"打开快速入门",
            foot:"提示：使用资源键可将帮助条目与测试自动化对齐。",
            groups:{menu:"菜单",toolbar:"工具栏与快捷操作",ctx:"右键菜单",props:"属性面板控件",options:"形状、样式与选项值",settings:"对话框、偏好与设置",other:"其他控件"},
            desc:{dialog:"对话框、提示或确认控件。",history:"历史控制（撤销/重做/重复）。",search:"搜索/筛选/替换控件。",save:"保存/导出控件。",remove:"删除/清空/取消链接操作。",toggle:"模式或状态切换控件。",create:"创建/添加/应用操作。",open:"打开/加载/选择/导入操作。",navigate:"导航/缩放/选择控件。",adjust:"调整数值、样式或布局参数。",reference:"与工作流相关的控件。"}
        },
        "ja":{
            title:"Letterist 完全ヘルプとコントロール参照",
            lead:"アプリの全インタラクション面を対象にした、網羅的で検索可能な参照です。",
            note:"コントロール名またはリソースキーで検索できます。制作トレーニングと自動化 QA マッピングに利用できます。",
            flowTitle:"推奨プロダクションフロー",
            flow:["最初に文書/ページ基準（サイズ・DPI・既定値）を設定。","密な台詞作業前にパネル構造と読順を確定。","画像を読み込み、重要要素（背景/パネル/フローティング）をロック。","吹き出しと文字スタイルを整え、次に尻尾/リンクを調整。","書き出し前に翻訳パスと QA パスを実施。","最終の形式/範囲/言語パッケージで書き出し。"],
            refTitle:"完全コントロールインデックス",
            refLead:"メニュー、ツールバー、コンテキストメニュー、プロパティ制御、スタイルオプション、ダイアログ/設定制御を含みます。",
            search:"ラベルまたはキーでコントロール検索...",
            expand:"すべて展開",
            collapse:"すべて折りたたむ",
            colControl:"コントロール",
            colKey:"リソースキー",
            colBehavior:"動作",
            quickText:"完全参照ではなく手順チェックリストが必要ですか？",
            quickLink:"クイックスタートを開く",
            foot:"ヒント: リソースキーを使うと、ヘルプとテスト自動化を対応づけられます。",
            groups:{menu:"メニュー",toolbar:"ツールバーとクイック操作",ctx:"コンテキストメニュー",props:"プロパティパネル制御",options:"形状・スタイル・オプション値",settings:"ダイアログ・設定・環境設定",other:"その他のコントロール"},
            desc:{dialog:"ダイアログ、入力、確認の制御。",history:"履歴制御（Undo/Redo/Repeat）。",search:"検索/フィルタ/置換制御。",save:"保存/書き出し制御。",remove:"削除/クリア/解除アクション。",toggle:"モード/状態の切り替え制御。",create:"作成/追加/適用アクション。",open:"開く/読み込む/選択/インポート操作。",navigate:"ナビゲーション/ズーム/選択制御。",adjust:"数値・スタイル・レイアウトを調整。",reference:"ワークフロー関連の制御。"}
        },
        "ko":{
            title:"Letterist 전체 도움말 및 컨트롤 참조",
            lead:"앱의 전체 상호작용 영역을 다루는 포괄적이고 검색 가능한 참조입니다.",
            note:"컨트롤 라벨 또는 리소스 키로 검색할 수 있습니다. 제작 교육과 자동화 QA 매핑에 유용합니다.",
            flowTitle:"권장 제작 워크플로",
            flow:["먼저 문서/페이지 기준(크기, DPI, 기본값)을 설정합니다.","밀집 대사 작업 전에 패널 구조와 읽기 순서를 확정합니다.","이미지를 로드하고 핵심 요소(배경/패널/플로팅)를 잠급니다.","말풍선/텍스트 스타일을 정리한 뒤 꼬리/링크를 조정합니다.","내보내기 전에 번역 패스와 QA 패스를 실행합니다.","최종 형식/범위/언어 패키징으로 내보냅니다."],
            refTitle:"전체 컨트롤 인덱스",
            refLead:"메뉴, 툴바, 컨텍스트 메뉴, 속성 컨트롤, 스타일 옵션, 대화상자/설정 컨트롤을 포함합니다.",
            search:"라벨 또는 키로 컨트롤 검색...",
            expand:"모두 펼치기",
            collapse:"모두 접기",
            colControl:"컨트롤",
            colKey:"리소스 키",
            colBehavior:"동작",
            quickText:"전체 참조 대신 절차형 체크리스트가 필요하신가요?",
            quickLink:"빠른 시작 열기",
            foot:"팁: 리소스 키를 사용하면 도움말과 테스트 자동화를 정확히 매핑할 수 있습니다.",
            groups:{menu:"메뉴",toolbar:"툴바 및 빠른 작업",ctx:"컨텍스트 메뉴",props:"속성 패널 컨트롤",options:"형태, 스타일, 옵션 값",settings:"대화상자, 환경설정 및 설정",other:"기타 컨트롤"},
            desc:{dialog:"대화상자/프롬프트/확인 컨트롤.",history:"기록 제어(실행 취소/다시 실행/반복).",search:"검색/필터/찾기 바꾸기 컨트롤.",save:"저장/내보내기 컨트롤.",remove:"삭제/정리/연결 해제 동작.",toggle:"모드/상태 전환 컨트롤.",create:"생성/추가/적용 동작.",open:"열기/불러오기/선택/가져오기 동작.",navigate:"탐색/확대/선택 컨트롤.",adjust:"수치/스타일/레이아웃 매개변수 조정.",reference:"워크플로 관련 컨트롤."}
        }
    };

    function categoryId(key){
        if (key.startsWith("menu.")) return "menu";
        if (key.startsWith("toolbar.tooltip.") || key.startsWith("sidebar.tooltip.") || key.startsWith("context.tooltip.") || key.startsWith("panel_layout.tooltip.") || key.startsWith("template.tooltip.") || key.startsWith("translation.tooltip.")) return "toolbar";
        if (key.startsWith("ctx.")) return "ctx";
        if (key.startsWith("props.tab.") || key.startsWith("props.header.") || key.startsWith("props.button.") || key.startsWith("props.label.") || key.startsWith("props.section.")) return "props";
        if (key.startsWith("shape.") || key.startsWith("tail.") || key.startsWith("text.fit.") || key.startsWith("text.overflow.") || key.startsWith("text.button.") || key.startsWith("warp.") || key.startsWith("fill.") || key.startsWith("line_style.") || key.startsWith("guide.") || key.startsWith("arrange.") || key.startsWith("align.") || key.startsWith("distribute.")) return "options";
        if (key.startsWith("prefs.field.") || key.startsWith("tools.docsettings.") || key.startsWith("tools.unit.") || key.startsWith("tools.color.") || key.startsWith("tools.dialog.") || key.startsWith("export.dialog.") || key.startsWith("font_chooser.") || key.startsWith("templates.dialog.") || key.startsWith("template.") || key.startsWith("input.dialog.") || key.startsWith("panel_layout.dialog.") || key.startsWith("delete.dialog.") || key.startsWith("find.") || key.startsWith("replace.")) return "settings";
        return "other";
    }

    function descType(key){
        if (key.includes(".dialog.")) return "dialog";
        if (/undo|redo|history|repeat/.test(key)) return "history";
        if (/find|search|replace|regex|match|filter/.test(key)) return "search";
        if (/save|export/.test(key)) return "save";
        if (/delete|remove|clear|unlink|ungroup/.test(key)) return "remove";
        if (/toggle|show|hide|visible|lock|unlock|enable|disable|snap|grid|guides|mode|layout/.test(key)) return "toggle";
        if (/new|add|create|duplicate|copy|paste|import|insert|apply/.test(key)) return "create";
        if (/open|load|browse|choose|select/.test(key)) return "open";
        if (/zoom|fit|pan|scroll|focus|selection/.test(key)) return "navigate";
        if (/width|height|x$|y$|angle|opacity|size|padding|spacing|ratio|dpi|quality|font|color|stroke|fill|shadow|glow|blur|offset|margin|blend|warp|anchor|align|distribute|line|curve|radius|gutter|safe|reading/.test(key)) return "adjust";
        return "reference";
    }

    const lang = normalizeLanguage(new URLSearchParams(location.search).get("lang"));
    const t = UI[lang] || UI.en;
    const labels = HELP_DATA.labels[lang] || HELP_DATA.labels.en;
    document.documentElement.lang = lang;
    document.title = t.title;

    function setText(id, value){ const e = document.getElementById(id); if (e) e.textContent = value; }
    setText("title", t.title);
    setText("lead", t.lead);
    setText("note", t.note);
    setText("flowTitle", t.flowTitle);
    setText("refTitle", t.refTitle);
    setText("refLead", t.refLead);
    setText("quickText", t.quickText);
    setText("quickLink", t.quickLink);
    setText("foot", t.foot);
    document.getElementById("search").placeholder = t.search;
    document.getElementById("expandAll").textContent = t.expand;
    document.getElementById("collapseAll").textContent = t.collapse;
    document.getElementById("quickLink").href = "../quickstart/index.html?lang=" + encodeURIComponent(lang);
    document.getElementById("flowList").innerHTML = t.flow.map(x => `<li>${x}</li>`).join("");

    const grouped = { menu:[], toolbar:[], ctx:[], props:[], options:[], settings:[], other:[] };
    for (const key of HELP_DATA.keys) {
        grouped[categoryId(key)].push(key);
    }

    const order = ["menu","toolbar","ctx","props","options","settings","other"];
    const host = document.getElementById("groups");
    const search = document.getElementById("search");

    function render(filter){
        const q = (filter || "").trim().toLowerCase();
        let html = "";
        for (const id of order) {
            const source = grouped[id];
            const rows = source.filter((k) => {
                if (!q) return true;
                const label = (labels[k] || k).toLowerCase();
                return k.toLowerCase().includes(q) || label.includes(q);
            });
            if (!rows.length) continue;
            html += `<details open><summary>${t.groups[id]}<span class="count">(${rows.length})</span></summary><div class="table-wrap"><table><thead><tr><th>${t.colControl}</th><th>${t.colKey}</th><th>${t.colBehavior}</th></tr></thead><tbody>`;
            html += rows.map((k) => `<tr><td>${labels[k] || k}</td><td><code>${k}</code></td><td>${t.desc[descType(k)] || t.desc.reference}</td></tr>`).join("");
            html += "</tbody></table></div></details>";
        }
        host.innerHTML = html || `<p>${t.search}</p>`;
    }

    document.getElementById("expandAll").addEventListener("click", () => {
        document.querySelectorAll("details").forEach((d) => d.open = true);
    });
    document.getElementById("collapseAll").addEventListener("click", () => {
        document.querySelectorAll("details").forEach((d) => d.open = false);
    });
    search.addEventListener("input", () => render(search.value));
    render("");
})();
</script>
</body>
</html>
'@

$html = $html.Replace("__DATA__", $jsonData)
Set-Content -Path $outPath -Value $html -Encoding utf8
Write-Host "Generated $outPath with $($keys.Count) controls and $($langs.Count) languages."
