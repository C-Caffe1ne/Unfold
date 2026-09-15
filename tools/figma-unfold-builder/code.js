const FONT_REGULAR = { family: "Inter", style: "Regular" };
const FONT_SEMIBOLD = { family: "Inter", style: "Semi Bold" };

let variables = {};
let ids = [];

function remember(node) {
  ids.push(node.id);
  return node;
}

function variable(name) {
  const result = variables[name];
  if (!result) throw new Error(`Missing variable: ${name}`);
  return result;
}

function boundPaint(name, opacity = 1) {
  return figma.variables.setBoundVariableForPaint(
    { type: "SOLID", color: { r: 0.4, g: 0.4, b: 0.4 }, opacity },
    "color",
    variable(name),
  );
}

function setRadius(node, name) {
  const radius = variable(name);
  node.setBoundVariable("topLeftRadius", radius);
  node.setBoundVariable("topRightRadius", radius);
  node.setBoundVariable("bottomLeftRadius", radius);
  node.setBoundVariable("bottomRightRadius", radius);
}

function setPadding(node, horizontalName = "Space/Control/Gap", verticalName = "Space/Stack/Default") {
  const horizontal = variable(horizontalName);
  const vertical = variable(verticalName);
  node.setBoundVariable("paddingLeft", horizontal);
  node.setBoundVariable("paddingRight", horizontal);
  node.setBoundVariable("paddingTop", vertical);
  node.setBoundVariable("paddingBottom", vertical);
}

function addText(parent, name, value, options = {}) {
  const node = remember(figma.createText());
  node.name = name;
  node.fontName = options.weight === "semibold" ? FONT_SEMIBOLD : FONT_REGULAR;
  node.characters = value;
  node.fontSize = options.size || 14;
  node.lineHeight = { unit: "PERCENT", value: options.lineHeight || 140 };
  node.fills = [boundPaint(options.color || "Color/Content/Primary", options.opacity || 1)];
  if (options.width) {
    node.textAutoResize = "HEIGHT";
    node.resize(options.width, Math.max(20, (options.size || 14) * 1.4));
  }
  parent.appendChild(node);
  return node;
}

function addHeading(root, title, description) {
  addText(root, "Title", title, { size: 32, weight: "semibold" });
  addText(root, "Description", description, {
    size: 14,
    width: 1180,
    color: "Color/Content/Muted",
  });
}

function createDocRoot(page, name, x, y, height = null) {
  const existing = page.children.find((node) => node.name === name);
  if (existing) return existing;

  const root = remember(figma.createFrame());
  root.name = name;
  root.layoutMode = "VERTICAL";
  root.primaryAxisSizingMode = height ? "FIXED" : "AUTO";
  root.counterAxisSizingMode = "FIXED";
  root.resize(1440, height || 1);
  root.paddingLeft = 80;
  root.paddingRight = 80;
  root.paddingTop = 64;
  root.paddingBottom = 64;
  root.itemSpacing = 28;
  root.fills = [boundPaint("Color/Background/Canvas")];
  root.x = x;
  root.y = y;
  page.appendChild(root);
  return root;
}

function findNodeByName(page, name, type) {
  return page.findAllWithCriteria({ types: [type] }).find((node) => node.name === name);
}

function styleForButton(style, state) {
  const disabled = state === "Disabled";
  if (style === "Primary") {
    return {
      fill: state === "Hover" ? "Color/Interaction/Primary Hover" : "Color/Content/Primary",
      text: "Color/Content/On Accent",
      stroke: "Color/Border/Transparent",
      opacity: disabled ? 0.45 : state === "Pressed" ? 0.86 : 1,
    };
  }
  if (style === "Secondary") {
    return {
      fill: state === "Hover" ? "Color/Interaction/Hover" : state === "Pressed" ? "Color/Background/Surface" : "Color/Background/Raised",
      text: disabled ? "Color/Content/Muted" : "Color/Content/Primary",
      stroke: "Color/Border/Default",
      opacity: disabled ? 0.45 : 1,
    };
  }
  if (style === "Danger") {
    return {
      fill: state === "Hover" || state === "Pressed" ? "Color/Status/Error" : "Color/Background/Surface",
      text: state === "Hover" || state === "Pressed" ? "Color/Content/On Accent" : "Color/Status/Error",
      stroke: "Color/Status/Error",
      opacity: disabled ? 0.45 : state === "Pressed" ? 0.84 : 1,
    };
  }
  return {
    fill: state === "Hover" ? "Color/Background/Surface" : state === "Pressed" ? "Color/Background/Raised" : null,
    text: disabled ? "Color/Content/Muted" : "Color/Content/Primary",
    stroke: "Color/Border/Transparent",
    opacity: disabled ? 0.45 : 1,
  };
}

function layoutVariantSet(set, columns, childWidth, childHeight, gapX = 24, gapY = 28) {
  const padding = 32;
  set.children.forEach((child, index) => {
    child.x = padding + (index % columns) * (childWidth + gapX);
    child.y = padding + Math.floor(index / columns) * (childHeight + gapY);
  });
  const rows = Math.ceil(set.children.length / columns);
  set.resize(
    padding * 2 + columns * childWidth + (columns - 1) * gapX,
    padding * 2 + rows * childHeight + (rows - 1) * gapY,
  );
  set.fills = [boundPaint("Color/Background/Shell")];
  set.strokes = [boundPaint("Color/Border/Default")];
  set.setBoundVariable("strokeWeight", variable("Border/Default"));
  setRadius(set, "Radius/Card");
}

function addUsage(root, lines) {
  const card = remember(figma.createFrame());
  card.name = "Usage";
  card.layoutMode = "VERTICAL";
  card.primaryAxisSizingMode = "AUTO";
  card.counterAxisSizingMode = "FIXED";
  card.resize(1280, 1);
  card.itemSpacing = 8;
  card.setBoundVariable("paddingLeft", variable("Space/Card/Inset"));
  card.setBoundVariable("paddingRight", variable("Space/Card/Inset"));
  card.setBoundVariable("paddingTop", variable("Space/Card/Inset"));
  card.setBoundVariable("paddingBottom", variable("Space/Card/Inset"));
  card.fills = [boundPaint("Color/Background/Surface")];
  setRadius(card, "Radius/Control");
  root.appendChild(card);
  lines.forEach((line) => addText(card, "Usage note", line, { size: 13, color: "Color/Content/Muted" }));
  return card;
}

async function ensureExtraTokens() {
  const collections = await figma.variables.getLocalVariableCollectionsAsync();
  const primitiveCollection = collections.find((item) => item.name === "Core Primitives");
  const semanticCollection = collections.find((item) => item.name === "Unfold Semantics");
  if (!primitiveCollection || !semanticCollection) throw new Error("Unfold variable collections are missing");

  const locals = await figma.variables.getLocalVariablesAsync();
  const primitiveMode = primitiveCollection.modes[0].modeId;
  const semanticMode = semanticCollection.modes[0].modeId;

  function ensurePrimitive(name, type, value, scopes, description, syntax) {
    let item = locals.find((entry) => entry.name === name && entry.variableCollectionId === primitiveCollection.id);
    if (!item) item = figma.variables.createVariable(name, primitiveCollection, type);
    item.scopes = scopes;
    item.description = description;
    item.setVariableCodeSyntax("WEB", syntax);
    item.setValueForMode(primitiveMode, value);
    return item;
  }

  const transparent = ensurePrimitive(
    "Color/Transparent",
    "COLOR",
    { r: 0, g: 0, b: 0, a: 0 },
    [],
    "Transparent button border",
    "var(--unfold-primitive-color-transparent)",
  );
  const one = ensurePrimitive("Size/1", "FLOAT", 1, [], "Default border width", "var(--unfold-primitive-size-1)");
  const two = ensurePrimitive("Size/2", "FLOAT", 2, [], "Button border width", "var(--unfold-primitive-size-2)");

  function ensureSemantic(name, type, source, scopes, description, syntax) {
    let item = locals.find((entry) => entry.name === name && entry.variableCollectionId === semanticCollection.id);
    if (!item) item = figma.variables.createVariable(name, semanticCollection, type);
    item.scopes = scopes;
    item.description = description;
    item.setVariableCodeSyntax("WEB", syntax);
    item.setValueForMode(semanticMode, figma.variables.createVariableAlias(source));
    return item;
  }

  ensureSemantic("Color/Border/Transparent", "COLOR", transparent, ["STROKE_COLOR"], "Stable transparent button border", "var(--unfold-color-border-transparent)");
  ensureSemantic("Border/Default", "FLOAT", one, ["STROKE_FLOAT"], "Inputs and cards", "var(--unfold-border-default)");
  ensureSemantic("Border/Button", "FLOAT", two, ["STROKE_FLOAT"], "Button style border", "var(--unfold-border-button)");
}

async function refreshVariables() {
  variables = Object.fromEntries(
    (await figma.variables.getLocalVariablesAsync())
      .filter((item) => item.variableCollectionId === "VariableCollectionId:3:2")
      .map((item) => [item.name, item]),
  );
}

function buildButton(page) {
  const existing = findNodeByName(page, "Button", "COMPONENT_SET");
  if (existing) return existing;

  const root = createDocRoot(page, "Components / Button", 1600, 620);
  addHeading(root, "Button", "텍스트 버튼의 시각 스타일과 상태를 한 Variant set에서 관리합니다.");
  addUsage(root, ["Style: Primary · Secondary · Quiet · Danger", "State: Default · Hover · Pressed · Disabled", "Label property에서 한글 라벨을 직접 편집합니다."]);

  const variants = [];
  const styles = ["Primary", "Secondary", "Quiet", "Danger"];
  const states = ["Default", "Hover", "Pressed", "Disabled"];
  for (const style of styles) {
    for (const state of states) {
      const visual = styleForButton(style, state);
      const component = remember(figma.createComponent());
      component.name = `Style=${style}, State=${state}`;
      component.description = "Unfold text action button";
      component.layoutMode = "HORIZONTAL";
      component.primaryAxisSizingMode = "FIXED";
      component.counterAxisSizingMode = "FIXED";
      component.primaryAxisAlignItems = "CENTER";
      component.counterAxisAlignItems = "CENTER";
      component.resize(112, 38);
      component.opacity = visual.opacity;
      component.fills = visual.fill ? [boundPaint(visual.fill)] : [];
      component.strokes = [boundPaint(visual.stroke)];
      component.setBoundVariable("strokeWeight", variable("Border/Button"));
      setRadius(component, "Radius/Control");
      setPadding(component);

      const label = addText(component, "Label", "버튼", { size: 14, weight: "semibold", color: visual.text });
      const labelKey = component.addComponentProperty("Label", "TEXT", "버튼");
      label.componentPropertyReferences = { characters: labelKey };
      variants.push(component);
    }
  }

  const set = remember(figma.combineAsVariants(variants, page));
  set.name = "Button";
  set.description = "Unfold button. Choose Style and State; edit Label in the instance properties.";
  layoutVariantSet(set, 4, 112, 38);
  root.appendChild(set);
  set.layoutSizingHorizontal = "FIXED";
  set.layoutSizingVertical = "FIXED";
  return set;
}

function iconComponents(page) {
  return Object.fromEntries(
    page
      .findAllWithCriteria({ types: ["COMPONENT"] })
      .filter((node) => node.name.startsWith("Icon/"))
      .map((node) => [node.name.slice(5), node]),
  );
}

function buildIconButton(page, icons) {
  const existing = findNodeByName(page, "Icon Button", "COMPONENT_SET");
  if (existing) return existing;
  if (!icons.Play) throw new Error("Icon/Play is missing");

  const root = createDocRoot(page, "Components / Icon Button", 3100, 0);
  addHeading(root, "Icon Button", "44×44 타이머·내비게이션 버튼입니다. Icon property에서 아이콘을 교체합니다.");
  addUsage(root, ["Style: Primary · Secondary · Quiet", "State: Default · Hover · Pressed · Disabled", "Icon: local system icon instance swap"]);

  const variants = [];
  const styles = ["Primary", "Secondary", "Quiet"];
  const states = ["Default", "Hover", "Pressed", "Disabled"];
  for (const style of styles) {
    for (const state of states) {
      const visual = styleForButton(style, state);
      const component = remember(figma.createComponent());
      component.name = `Style=${style}, State=${state}`;
      component.description = "Unfold 44px icon action";
      component.layoutMode = "HORIZONTAL";
      component.primaryAxisSizingMode = "FIXED";
      component.counterAxisSizingMode = "FIXED";
      component.primaryAxisAlignItems = "CENTER";
      component.counterAxisAlignItems = "CENTER";
      component.resize(44, 44);
      component.setBoundVariable("width", variable("Size/Icon Button"));
      component.setBoundVariable("height", variable("Size/Icon Button"));
      component.opacity = visual.opacity;
      component.fills = visual.fill ? [boundPaint(visual.fill)] : [];
      component.strokes = [boundPaint(visual.stroke)];
      component.setBoundVariable("strokeWeight", variable("Border/Button"));
      setRadius(component, "Radius/Control");

      const icon = remember(icons.Play.createInstance());
      icon.name = "Icon";
      component.appendChild(icon);
      const iconKey = component.addComponentProperty("Icon", "INSTANCE_SWAP", icons.Play.id);
      icon.componentPropertyReferences = { mainComponent: iconKey };
      variants.push(component);
    }
  }

  const set = remember(figma.combineAsVariants(variants, page));
  set.name = "Icon Button";
  set.description = "Unfold icon button. Use instance swap for Play, Pause, Stop, navigation, and settings icons.";
  layoutVariantSet(set, 4, 44, 44, 28, 28);
  root.appendChild(set);
  set.layoutSizingHorizontal = "FIXED";
  set.layoutSizingVertical = "FIXED";
  return set;
}

function buildNumericInput(page, icons) {
  const existing = findNodeByName(page, "Numeric Input", "COMPONENT_SET");
  if (existing) return existing;
  if (!icons["Chevron Up"] || !icons["Chevron Down"]) throw new Error("Chevron icons are missing");

  const root = createDocRoot(page, "Components / Numeric Input", 3100, 820);
  addHeading(root, "Numeric Input", "외곽 컨테이너만 radius와 border를 소유해 비활성화 상태에서도 틈이 생기지 않습니다.");
  addUsage(root, ["State: Default · Focused · Disabled", "Focused는 hover/focus 색 변화 없이 Default와 같은 외곽선을 유지합니다.", "Value property에서 숫자를 왼쪽·세로 중앙 정렬로 편집합니다."]);

  const variants = [];
  for (const state of ["Default", "Focused", "Disabled"]) {
    const component = remember(figma.createComponent());
    component.name = `State=${state}`;
    component.description = "Unfold minute NumericUpDown visual contract";
    component.layoutMode = "HORIZONTAL";
    component.primaryAxisSizingMode = "FIXED";
    component.counterAxisSizingMode = "FIXED";
    component.counterAxisAlignItems = "CENTER";
    component.resize(124, 38);
    component.clipsContent = true;
    component.fills = [boundPaint("Color/Background/Shell")];
    component.strokes = [boundPaint("Color/Border/Default")];
    component.setBoundVariable("strokeWeight", variable("Border/Default"));
    setRadius(component, "Radius/Control");
    component.opacity = state === "Disabled" ? 0.48 : 1;

    const valueFrame = remember(figma.createFrame());
    valueFrame.name = "Value field";
    valueFrame.layoutMode = "HORIZONTAL";
    valueFrame.primaryAxisSizingMode = "FIXED";
    valueFrame.counterAxisSizingMode = "FIXED";
    valueFrame.primaryAxisAlignItems = "MIN";
    valueFrame.counterAxisAlignItems = "CENTER";
    valueFrame.resize(56, 38);
    valueFrame.paddingLeft = 10;
    valueFrame.fills = [];
    component.appendChild(valueFrame);
    const valueText = addText(valueFrame, "Value", "25", { size: 14, weight: "semibold", color: state === "Disabled" ? "Color/Content/Muted" : "Color/Content/Primary" });
    const valueKey = component.addComponentProperty("Value", "TEXT", "25");
    valueText.componentPropertyReferences = { characters: valueKey };

    for (const [name, iconComponent] of [["Increment", icons["Chevron Up"]], ["Decrement", icons["Chevron Down"]]]) {
      const divider = remember(figma.createRectangle());
      divider.name = "Divider";
      divider.resize(1, 38);
      divider.fills = [boundPaint("Color/Border/Default")];
      component.appendChild(divider);

      const arrow = remember(figma.createFrame());
      arrow.name = name;
      arrow.layoutMode = "HORIZONTAL";
      arrow.primaryAxisSizingMode = "FIXED";
      arrow.counterAxisSizingMode = "FIXED";
      arrow.primaryAxisAlignItems = "CENTER";
      arrow.counterAxisAlignItems = "CENTER";
      arrow.resize(name === "Increment" ? 33 : 33, 38);
      arrow.fills = [];
      component.appendChild(arrow);
      const icon = remember(iconComponent.createInstance());
      icon.resize(18, 18);
      arrow.appendChild(icon);
    }
    variants.push(component);
  }

  const set = remember(figma.combineAsVariants(variants, page));
  set.name = "Numeric Input";
  set.description = "Unfold numeric minute input. Outer frame owns radius and stroke; inner fields stay square and transparent.";
  layoutVariantSet(set, 3, 124, 38, 32, 28);
  root.appendChild(set);
  set.layoutSizingHorizontal = "FIXED";
  set.layoutSizingVertical = "FIXED";
  return set;
}

function buildStatusBadge(page) {
  const existing = findNodeByName(page, "Status Badge", "COMPONENT_SET");
  if (existing) return existing;

  const root = createDocRoot(page, "Components / Status Badge", 4600, 0);
  addHeading(root, "Status Badge", "타이머 상태를 색상과 텍스트로 함께 표시해 진행·일시정지·중지를 즉시 구분합니다.");
  addUsage(root, ["Running: Cream", "Paused · Idle · Break: Warning", "Stopped: Error"]);

  const specs = [
    ["Running", "진행 중", "Color/Content/Primary"],
    ["Paused", "일시정지", "Color/Status/Warning"],
    ["Stopped", "중지됨", "Color/Status/Error"],
    ["Idle", "대기 중", "Color/Status/Warning"],
    ["Break", "휴식 중", "Color/Status/Warning"],
  ];
  const variants = [];
  for (const [state, labelValue, color] of specs) {
    const component = remember(figma.createComponent());
    component.name = `State=${state}`;
    component.layoutMode = "HORIZONTAL";
    component.primaryAxisSizingMode = "AUTO";
    component.counterAxisSizingMode = "AUTO";
    component.counterAxisAlignItems = "CENTER";
    component.itemSpacing = 8;
    component.paddingLeft = 10;
    component.paddingRight = 10;
    component.paddingTop = 6;
    component.paddingBottom = 6;
    component.fills = [boundPaint("Color/Background/Surface")];
    setRadius(component, "Radius/Control");
    const dot = remember(figma.createEllipse());
    dot.name = "Status dot";
    dot.resize(8, 8);
    dot.fills = [boundPaint(color)];
    component.appendChild(dot);
    const label = addText(component, "Label", labelValue, { size: 12, weight: "semibold", color });
    const labelKey = component.addComponentProperty("Label", "TEXT", labelValue);
    label.componentPropertyReferences = { characters: labelKey };
    variants.push(component);
  }

  const set = remember(figma.combineAsVariants(variants, page));
  set.name = "Status Badge";
  set.description = "Timer state indicator with redundant text and color cues.";
  layoutVariantSet(set, 5, 92, 34, 20, 24);
  root.appendChild(set);
  set.layoutSizingHorizontal = "FIXED";
  set.layoutSizingVertical = "FIXED";
  return set;
}

function buildCard(page) {
  const existing = findNodeByName(page, "Card", "COMPONENT_SET");
  if (existing) return existing;

  const root = createDocRoot(page, "Components / Card", 4600, 700);
  addHeading(root, "Card", "설정·타이머·펫 영역에 사용하는 Surface와 Raised 컨테이너입니다.");
  addUsage(root, ["Style: Surface · Raised", "Title과 Description property를 수정합니다.", "Card radius 24px, inset 20px"]);

  const variants = [];
  for (const style of ["Surface", "Raised"]) {
    const component = remember(figma.createComponent());
    component.name = `Style=${style}`;
    component.layoutMode = "VERTICAL";
    component.primaryAxisSizingMode = "FIXED";
    component.counterAxisSizingMode = "FIXED";
    component.resize(360, 180);
    component.itemSpacing = 12;
    component.setBoundVariable("itemSpacing", variable("Space/Control/Gap"));
    component.setBoundVariable("paddingLeft", variable("Space/Card/Inset"));
    component.setBoundVariable("paddingRight", variable("Space/Card/Inset"));
    component.setBoundVariable("paddingTop", variable("Space/Card/Inset"));
    component.setBoundVariable("paddingBottom", variable("Space/Card/Inset"));
    component.fills = [boundPaint(style === "Surface" ? "Color/Background/Surface" : "Color/Background/Raised")];
    setRadius(component, "Radius/Card");
    const title = addText(component, "Title", "카드 제목", { size: 18, weight: "semibold" });
    const titleKey = component.addComponentProperty("Title", "TEXT", "카드 제목");
    title.componentPropertyReferences = { characters: titleKey };
    const description = addText(component, "Description", "설명과 컨트롤을 배치하는 기본 컨테이너", { size: 14, color: "Color/Content/Muted", width: 320 });
    const descriptionKey = component.addComponentProperty("Description", "TEXT", "설명과 컨트롤을 배치하는 기본 컨테이너");
    description.componentPropertyReferences = { characters: descriptionKey };
    variants.push(component);
  }

  const set = remember(figma.combineAsVariants(variants, page));
  set.name = "Card";
  set.description = "Unfold content container for settings, timer, and companion areas.";
  layoutVariantSet(set, 2, 360, 180, 32, 28);
  root.appendChild(set);
  set.layoutSizingHorizontal = "FIXED";
  set.layoutSizingVertical = "FIXED";
  return set;
}

function buildNavigationItem(page, icons) {
  const existing = findNodeByName(page, "Navigation Item", "COMPONENT_SET");
  if (existing) return existing;
  if (!icons.Settings) throw new Error("Icon/Settings is missing");

  const root = createDocRoot(page, "Components / Navigation Item", 6100, 0);
  addHeading(root, "Navigation Item", "64px 레일 안에서 사용하는 44×44 아이콘 내비게이션 항목입니다.");
  addUsage(root, ["State: Default · Hover · Selected · Disabled", "Icon property에서 Pet · Timer · Review · Settings를 선택합니다."]);

  const variants = [];
  for (const state of ["Default", "Hover", "Selected", "Disabled"]) {
    const component = remember(figma.createComponent());
    component.name = `State=${state}`;
    component.layoutMode = "HORIZONTAL";
    component.primaryAxisSizingMode = "FIXED";
    component.counterAxisSizingMode = "FIXED";
    component.primaryAxisAlignItems = "CENTER";
    component.counterAxisAlignItems = "CENTER";
    component.resize(44, 44);
    component.setBoundVariable("width", variable("Size/Icon Button"));
    component.setBoundVariable("height", variable("Size/Icon Button"));
    component.fills = state === "Hover" ? [boundPaint("Color/Interaction/Hover")] : state === "Selected" ? [boundPaint("Color/Background/Raised")] : [];
    component.opacity = state === "Disabled" ? 0.45 : 1;
    setRadius(component, "Radius/Control");
    const icon = remember(icons.Settings.createInstance());
    icon.name = "Icon";
    component.appendChild(icon);
    const iconKey = component.addComponentProperty("Icon", "INSTANCE_SWAP", icons.Settings.id);
    icon.componentPropertyReferences = { mainComponent: iconKey };
    variants.push(component);
  }

  const set = remember(figma.combineAsVariants(variants, page));
  set.name = "Navigation Item";
  set.description = "Icon-only navigation item for the 64px settings rail.";
  layoutVariantSet(set, 4, 44, 44, 28, 24);
  root.appendChild(set);
  set.layoutSizingHorizontal = "FIXED";
  set.layoutSizingVertical = "FIXED";
  return set;
}

function variantOf(set, properties) {
  const match = set.children.find((child) =>
    Object.entries(properties).every(([key, value]) => child.variantProperties && child.variantProperties[key] === value),
  );
  if (!match) throw new Error(`Variant not found in ${set.name}: ${JSON.stringify(properties)}`);
  return match;
}

function instanceOf(set, properties, text = null, icon = null) {
  const instance = remember(variantOf(set, properties).createInstance());
  if (text !== null) {
    const labelKey = Object.keys(instance.componentProperties).find((key) => key.startsWith("Label#") || key.startsWith("Value#"));
    if (labelKey) instance.setProperties({ [labelKey]: text });
  }
  if (icon) {
    const iconKey = Object.keys(instance.componentProperties).find((key) => key.startsWith("Icon#"));
    if (iconKey) instance.setProperties({ [iconKey]: icon.id });
  }
  return instance;
}

function dashboardCard(parent, name, width, height, style = "surface") {
  const card = remember(figma.createFrame());
  card.name = name;
  card.layoutMode = "VERTICAL";
  card.primaryAxisSizingMode = "FIXED";
  card.counterAxisSizingMode = "FIXED";
  card.resize(width, height);
  card.itemSpacing = 16;
  card.setBoundVariable("paddingLeft", variable("Space/Card/Inset"));
  card.setBoundVariable("paddingRight", variable("Space/Card/Inset"));
  card.setBoundVariable("paddingTop", variable("Space/Card/Inset"));
  card.setBoundVariable("paddingBottom", variable("Space/Card/Inset"));
  card.fills = [boundPaint(style === "raised" ? "Color/Background/Raised" : "Color/Background/Surface")];
  setRadius(card, "Radius/Card");
  parent.appendChild(card);
  return card;
}

function buildDashboard(page, sets, icons) {
  const existing = page.children.find((node) => node.name === "Settings Dashboard / Desktop 1120");
  if (existing) {
    const rail = existing.findAll((node) => node.name === "Navigation Rail")[0];
    if (rail && "children" in rail) {
      for (const child of [...rail.children]) {
        if (child.type === "INSTANCE" && child.name === "Navigation Item") child.remove();
      }
      for (const [iconName, state] of [["Timer", "Selected"], ["Settings", "Default"], ["Review", "Default"]]) {
        rail.appendChild(instanceOf(sets.navigation, { State: state }, null, icons[iconName]));
      }
    }
    const reminder = existing.findAll((node) => node.name === "알림 설정")[0];
    if (reminder && "itemSpacing" in reminder) {
      reminder.resize(300, 224);
      reminder.itemSpacing = 8;
      const fieldRow = reminder.findAll((node) => node.name === "Reminder Fields")[0];
      if (fieldRow && "layoutMode" in fieldRow) {
        fieldRow.primaryAxisSizingMode = "FIXED";
        fieldRow.counterAxisSizingMode = "FIXED";
        fieldRow.resize(260, 64);
        for (const field of fieldRow.children) {
          if ("layoutMode" in field) {
            field.primaryAxisSizingMode = "FIXED";
            field.counterAxisSizingMode = "FIXED";
            field.resize(124, 64);
          }
        }
      }
    }
    const routine = existing.findAll((node) => node.name === "루틴")[0];
    if (routine && "resize" in routine) routine.resize(300, 172);
    const review = existing.findAll((node) => node.name === "오늘의 기록")[0];
    if (review && "resize" in review) review.resize(300, 312);
    const status = existing.findAllWithCriteria({ types: ["INSTANCE"] }).find((node) => node.name === "Status Badge");
    if (status) {
      const labelKey = Object.keys(status.componentProperties).find((key) => key.startsWith("Label#"));
      if (labelKey) status.setProperties({ [labelKey]: "일시정지" });
    }
    return existing;
  }

  const canvas = remember(figma.createFrame());
  canvas.name = "Settings Dashboard / Desktop 1120";
  canvas.resize(1120, 800);
  canvas.fills = [boundPaint("Color/Background/Canvas")];
  canvas.x = 0;
  canvas.y = 0;
  page.appendChild(canvas);

  const shell = remember(figma.createFrame());
  shell.name = "App Shell";
  shell.layoutMode = "HORIZONTAL";
  shell.primaryAxisSizingMode = "FIXED";
  shell.counterAxisSizingMode = "FIXED";
  shell.resize(1088, 768);
  shell.paddingLeft = 14;
  shell.paddingRight = 14;
  shell.paddingTop = 14;
  shell.paddingBottom = 14;
  shell.itemSpacing = 16;
  shell.fills = [boundPaint("Color/Background/Shell")];
  shell.strokes = [boundPaint("Color/Border/Default")];
  shell.setBoundVariable("strokeWeight", variable("Border/Default"));
  setRadius(shell, "Radius/Frame");
  shell.x = 16;
  shell.y = 16;
  canvas.appendChild(shell);

  const rail = remember(figma.createFrame());
  rail.name = "Navigation Rail";
  rail.layoutMode = "VERTICAL";
  rail.primaryAxisSizingMode = "FIXED";
  rail.counterAxisSizingMode = "FIXED";
  rail.resize(64, 740);
  rail.setBoundVariable("width", variable("Size/Navigation Width"));
  rail.paddingTop = 10;
  rail.itemSpacing = 12;
  rail.counterAxisAlignItems = "CENTER";
  rail.fills = [boundPaint("Color/Background/Surface")];
  setRadius(rail, "Radius/Card");
  shell.appendChild(rail);
  for (const [iconName, state] of [["Timer", "Selected"], ["Settings", "Default"], ["Review", "Default"]]) {
    const item = instanceOf(sets.navigation, { State: state }, null, icons[iconName]);
    rail.appendChild(item);
  }

  const main = remember(figma.createFrame());
  main.name = "Main Content";
  main.layoutMode = "HORIZONTAL";
  main.primaryAxisSizingMode = "FIXED";
  main.counterAxisSizingMode = "FIXED";
  main.resize(980, 740);
  main.itemSpacing = 16;
  main.fills = [];
  shell.appendChild(main);

  const center = remember(figma.createFrame());
  center.name = "Companion and Timer";
  center.layoutMode = "VERTICAL";
  center.primaryAxisSizingMode = "FIXED";
  center.counterAxisSizingMode = "FIXED";
  center.resize(664, 740);
  center.itemSpacing = 16;
  center.fills = [];
  main.appendChild(center);

  const companion = dashboardCard(center, "Companion Card", 664, 470, "raised");
  companion.primaryAxisAlignItems = "CENTER";
  companion.counterAxisAlignItems = "CENTER";
  companion.itemSpacing = 10;
  addText(companion, "Pet", "◢  ◣", { size: 64, weight: "semibold" });
  addText(companion, "Name", "보리", { size: 18, weight: "semibold" });
  addText(companion, "Message", "집중하는 동안 곁을 지켜볼게요.", { size: 14, color: "Color/Content/Muted" });

  const timer = dashboardCard(center, "Timer Card", 664, 254, "surface");
  timer.primaryAxisAlignItems = "CENTER";
  timer.counterAxisAlignItems = "CENTER";
  timer.itemSpacing = 12;
  addText(timer, "Section", "집중 타이머", { size: 14, weight: "semibold", color: "Color/Content/Muted" });
  addText(timer, "Time", "25:00", { size: 64, weight: "semibold" });
  const status = instanceOf(sets.status, { State: "Paused" }, "일시정지");
  timer.appendChild(status);
  const actions = remember(figma.createFrame());
  actions.name = "Timer Actions";
  actions.layoutMode = "HORIZONTAL";
  actions.primaryAxisSizingMode = "AUTO";
  actions.counterAxisSizingMode = "AUTO";
  actions.itemSpacing = 12;
  actions.fills = [];
  timer.appendChild(actions);
  const play = instanceOf(sets.iconButton, { Style: "Primary", State: "Default" }, null, icons.Play);
  const stop = instanceOf(sets.iconButton, { Style: "Secondary", State: "Default" }, null, icons.Stop);
  actions.appendChild(play);
  actions.appendChild(stop);

  const right = remember(figma.createFrame());
  right.name = "Settings Detail";
  right.layoutMode = "VERTICAL";
  right.primaryAxisSizingMode = "FIXED";
  right.counterAxisSizingMode = "FIXED";
  right.resize(300, 740);
  right.itemSpacing = 16;
  right.fills = [];
  main.appendChild(right);

  const reminder = dashboardCard(right, "알림 설정", 300, 224, "surface");
  reminder.itemSpacing = 8;
  const reminderHeader = remember(figma.createFrame());
  reminderHeader.name = "Header";
  reminderHeader.layoutMode = "HORIZONTAL";
  reminderHeader.primaryAxisSizingMode = "FIXED";
  reminderHeader.counterAxisSizingMode = "AUTO";
  reminderHeader.resize(260, 38);
  reminderHeader.primaryAxisAlignItems = "SPACE_BETWEEN";
  reminderHeader.counterAxisAlignItems = "CENTER";
  reminderHeader.fills = [];
  reminder.appendChild(reminderHeader);
  addText(reminderHeader, "Title", "알림 설정", { size: 18, weight: "semibold" });
  const apply = instanceOf(sets.button, { Style: "Primary", State: "Default" }, "적용");
  apply.resize(64, 38);
  reminderHeader.appendChild(apply);
  const fieldRow = remember(figma.createFrame());
  fieldRow.name = "Reminder Fields";
  fieldRow.layoutMode = "HORIZONTAL";
  fieldRow.primaryAxisSizingMode = "FIXED";
  fieldRow.counterAxisSizingMode = "FIXED";
  fieldRow.resize(260, 64);
  fieldRow.itemSpacing = 12;
  fieldRow.fills = [];
  reminder.appendChild(fieldRow);
  for (const [label, state, value] of [["알림 간격 (분)", "Disabled", "25"], ["자리 비움 (분)", "Default", "15"]]) {
    const field = remember(figma.createFrame());
    field.name = label;
    field.layoutMode = "VERTICAL";
    field.primaryAxisSizingMode = "FIXED";
    field.counterAxisSizingMode = "FIXED";
    field.resize(124, 64);
    field.itemSpacing = 6;
    field.fills = [];
    fieldRow.appendChild(field);
    addText(field, "Label", label, { size: 11, color: "Color/Content/Muted" });
    const input = instanceOf(sets.numeric, { State: state }, value);
    field.appendChild(input);
  }
  addText(reminder, "Helper", "시간을 바꾸려면 타이머를 일시정지하거나 정지해 주세요.", { size: 11, color: "Color/Content/Muted", width: 260 });

  const routine = dashboardCard(right, "루틴", 300, 172, "surface");
  addText(routine, "Title", "루틴", { size: 18, weight: "semibold" });
  addText(routine, "Description", "25분 집중 · 5분 휴식\n긴 휴식은 4회마다", { size: 14, color: "Color/Content/Muted", width: 260 });
  const routineButton = instanceOf(sets.button, { Style: "Secondary", State: "Default" }, "루틴 편집");
  routine.appendChild(routineButton);

  const review = dashboardCard(right, "오늘의 기록", 300, 312, "surface");
  addText(review, "Title", "오늘의 기록", { size: 18, weight: "semibold" });
  addText(review, "Value", "3회", { size: 40, weight: "semibold" });
  addText(review, "Description", "총 75분 집중\n다음 목표까지 25분", { size: 14, color: "Color/Content/Muted", width: 260 });
  const reviewButton = instanceOf(sets.button, { Style: "Quiet", State: "Default" }, "기록 보기");
  review.appendChild(reviewButton);

  return canvas;
}

async function main() {
  await Promise.all([figma.loadFontAsync(FONT_REGULAR), figma.loadFontAsync(FONT_SEMIBOLD)]);
  await ensureExtraTokens();
  await refreshVariables();

  const designPage = figma.root.children.find((page) => page.name === "01 · Design System");
  const dashboardPage = figma.root.children.find((page) => page.name === "02 · Settings Dashboard");
  if (!designPage || !dashboardPage) throw new Error("Expected Unfold pages are missing");

  await figma.setCurrentPageAsync(designPage);
  const icons = iconComponents(designPage);
  const sets = {
    button: buildButton(designPage),
    iconButton: buildIconButton(designPage, icons),
    numeric: buildNumericInput(designPage, icons),
    status: buildStatusBadge(designPage),
    card: buildCard(designPage),
    navigation: buildNavigationItem(designPage, icons),
  };
  for (const section of designPage.children.filter(
    (node) => node.type === "FRAME" && node.name.startsWith("Components / "),
  )) {
    section.primaryAxisSizingMode = "AUTO";
    section.counterAxisSizingMode = "FIXED";
  }

  await figma.setCurrentPageAsync(dashboardPage);
  const dashboard = buildDashboard(dashboardPage, sets, icons);
  figma.currentPage.selection = [dashboard];
  figma.viewport.scrollAndZoomIntoView([dashboard]);
  figma.closePlugin(`Unfold design system complete · ${ids.length} nodes created`);
}

main().catch((error) => {
  console.error(error);
  figma.closePlugin(`Unfold builder error: ${error.message}`);
});
