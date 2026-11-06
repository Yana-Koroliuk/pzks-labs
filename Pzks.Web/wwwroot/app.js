"use strict";

const $ = id => document.getElementById(id);

const groupTitles = {
    mandatory: "Обов'язкові",
    brackets: "Дужки",
    constants: "Константи",
    simplification: "Спрощення",
};

const stepTitles = {
    brackets: "дужки",
    simplification: "спрощення",
    constants: "константи",
    regrouping: "перегрупування",
};

const memoExample = { id: "Приклад", expression: "a+b*c + k - x - d - e - f/g/h/q", description: "Приклад з пам'ятки" };

let current = null;

function el(tag, attributes = {}, ...children) {
    const node = document.createElement(tag);
    for (const [key, value] of Object.entries(attributes)) {
        if (key === "class") node.className = value;
        else if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
        else node.setAttribute(key, value);
    }
    for (const child of children)
        node.append(child instanceof Node ? child : String(child));
    return node;
}

function show(id, visible) {
    $(id).hidden = !visible;
}

// ---------- Тести з пам'ятки ----------

async function loadSamples() {
    const samples = await (await fetch("/api/samples")).json();
    const groups = {};
    for (const sample of samples)
        (groups[sample.group] ??= []).push(sample);

    const container = $("samples");
    for (const [group, items] of Object.entries(groups)) {
        const box = el("div", { class: "sample-group" }, el("span", { class: "group-title" }, groupTitles[group] ?? group));
        for (const sample of items)
            box.append(sampleChip(sample, group));
        container.append(box);
    }
    container.append(el("div", { class: "sample-group" },
        el("span", { class: "group-title" }, "Пам'ятка"),
        sampleChip(memoExample, "")));
}

function sampleChip(sample, group) {
    return el("button", {
        type: "button",
        class: `chip ${group}`,
        title: `${sample.description ? sample.description + ": " : ""}${sample.expression}`,
        onclick: () => build(sample.expression),
    }, sample.id);
}

// ---------- Побудова ----------

async function build(expression) {
    $("expression").value = expression;
    history.replaceState(null, "", "#" + encodeURIComponent(expression));

    const response = await fetch("/api/parallel", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expression }),
    });
    current = await response.json();
    render(current);
}

function render(r) {
    show("result", true);
    const lr1Valid = r.lr1Errors.length === 0;

    const status = $("status");
    status.className = "card status " + (r.valid ? "ok" : "bad");
    status.textContent = !lr1Valid
        ? `Вираз не пройшов перевірку ЛР1: помилок ${r.lr1Errors.length}. Дерево паралельної форми не будується.`
        : r.errors.length > 0
            ? "Перевірка ЛР1: помилок немає. Але вираз містить ділення на 0."
            : "Перевірка ЛР1: помилок немає. Дерево паралельної форми побудовано.";

    renderLr1(r, lr1Valid);
    renderMetrics(r);
    renderForms(r);
    renderSteps(r);
    renderZeroErrors(r);
    renderTrees(r);
    renderLevels(r);
}

function renderLr1(r, lr1Valid) {
    show("lr1", !lr1Valid);
    if (lr1Valid) return;

    const colors = new Array(r.expression.length).fill(null);
    for (const error of r.lr1Errors)
        for (let i = error.position; i < Math.min(r.expression.length, error.position + Math.max(1, error.length)); i++)
            colors[i] ??= error.stage;

    const ruler = Array.from({ length: r.expression.length }, (_, i) => i % 10).join("");
    const line = $("lr1-expression");
    line.replaceChildren(el("div", { class: "ruler" }, ruler));
    const text = el("div");
    [...r.expression].forEach((ch, i) => text.append(colors[i] ? el("span", { class: colors[i] }, ch) : ch));
    line.append(text);

    $("lr1-errors").replaceChildren(...r.lr1Errors.map(e =>
        el("li", { class: e.stage }, `[${e.stage === "lexical" ? "лекс." : "синт."}] позиція ${e.position}: ${e.message}`)));

    const correction = $("lr1-correction");
    correction.replaceChildren();
    if (r.correction?.valid) {
        correction.append(el("div", { class: "correction" },
            "Автовиправлення:", el("code", {}, r.correction.expression),
            el("button", { type: "button", class: "small", onclick: () => build(r.correction.expression) }, "Використати")));
    }
}

function renderMetrics(r) {
    const s = r.source, p = r.parallel;
    show("metrics", !!(s && p));
    if (!s || !p) return;

    const metric = (name, before, after, hint = "") => el("div", { class: "metric" },
        el("div", { class: "name" }, name),
        el("div", { class: "value" }, String(before), el("span", { class: "arrow" }, "→"), el("span", { class: "after" }, String(after))),
        el("div", { class: "hint" }, hint));

    $("metrics").replaceChildren(
        metric("Ярусів", s.tiers, p.tiers, "довжина дерева, такти"),
        metric("Операцій", s.operations, p.operations, "спрощення зменшує кількість"),
        metric("Ширина", s.width, p.width, "макс. операцій на ярусі"));
}

function renderForms(r) {
    show("forms", !!r.source);
    if (!r.source) return;
    $("source-infix").textContent = r.source.infix;
    show("simplified-row", r.simplified != null);
    show("parallel-row", r.parallel != null);
    $("simplified").textContent = r.simplified ?? "";
    $("parallel-infix").textContent = r.parallel?.infix ?? "";
}

function renderSteps(r) {
    show("steps-card", !!r.source);
    const steps = $("steps");
    if (r.steps.length === 0) {
        steps.replaceChildren(el("li", { class: "muted" }, "перетворень немає — дерево вже оптимальне"));
        return;
    }
    steps.replaceChildren(...r.steps.map(s =>
        el("li", {}, el("span", { class: `badge ${s.kind}` }, stepTitles[s.kind] ?? s.kind), s.description)));
}

function renderZeroErrors(r) {
    show("zero-card", r.errors.length > 0);
    $("zero-errors").replaceChildren(...r.errors.map(e =>
        el("li", {}, "терм ", el("code", {}, e.term), ": ", e.message)));
}

function renderTrees(r) {
    const visible = !!(r.source && r.parallel);
    show("parallel-card", visible);
    show("source-card", visible);
    if (!visible) return;
    $("source-svg").innerHTML = r.source.svg;
    $("parallel-svg").innerHTML = r.parallel.svg;
    $("source-tiers").textContent = `· ярусів: ${r.source.tiers}`;
    $("parallel-tiers").textContent = `· ярусів: ${r.parallel.tiers}`;
}

function renderLevels(r) {
    const levels = r.parallel?.levels ?? [];
    show("levels-card", levels.length > 0);
    $("levels").replaceChildren(...levels.map((level, i) =>
        el("tr", {},
            el("th", {}, `Ярус ${i + 1}`, el("div", { class: "muted" }, `${level.length} оп.`)),
            el("td", {}, ...level.map(op =>
                el("span", { class: "op", title: op.expression }, el("b", {}, `[${op.number}]`), " ", op.text))))));
}

// ---------- Збереження малюнків ----------

function download(blob, name) {
    const link = el("a", { href: URL.createObjectURL(blob), download: name });
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href), 1000);
}

function saveTree(which, format) {
    const svg = current?.[which]?.svg;
    if (!svg) return;
    const name = `lr2-${which === "source" ? "sequential" : "parallel"}`;
    const svgBlob = new Blob([svg], { type: "image/svg+xml;charset=utf-8" });
    if (format === "svg") {
        download(svgBlob, name + ".svg");
        return;
    }

    const image = new Image();
    image.onload = () => {
        const scale = 2;
        const canvas = el("canvas", { width: image.width * scale, height: image.height * scale });
        const context = canvas.getContext("2d");
        context.scale(scale, scale);
        context.drawImage(image, 0, 0);
        canvas.toBlob(blob => download(blob, name + ".png"));
    };
    image.src = URL.createObjectURL(svgBlob);
}

// ---------- Старт ----------

$("form").addEventListener("submit", event => {
    event.preventDefault();
    const expression = $("expression").value.trim();
    if (expression) build(expression);
});

document.querySelectorAll("[data-download]").forEach(button =>
    button.addEventListener("click", () => saveTree(button.dataset.download, button.dataset.format)));

loadSamples();
const fromHash = decodeURIComponent(location.hash.slice(1));
build(fromHash || "a-b-c-d-e-f-g-h");
