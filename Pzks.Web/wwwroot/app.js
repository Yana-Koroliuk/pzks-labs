"use strict";

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
            box.append(sampleChip(sample, group, () => build(sample.expression)));
        container.append(box);
    }
    container.append(el("div", { class: "sample-group" },
        el("span", { class: "group-title" }, "Пам'ятка"),
        sampleChip(memoExample, "", () => build(memoExample.expression))));
}

// ---------- Побудова ----------

async function build(expression) {
    $("expression").value = expression;
    history.replaceState(null, "", "#" + encodeURIComponent(expression));
    current = await postJson("/api/parallel", { expression });
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

    renderLr1(r, build);
    renderMetrics(r);
    renderForms(r);
    renderSteps(r);
    renderZeroErrors(r);
    renderTrees(r);
    const levels = r.parallel?.levels ?? [];
    show("levels-card", levels.length > 0);
    renderLevels("levels", levels);
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

function saveTree(which, format) {
    saveSvg(current?.[which]?.svg, `lr2-${which === "source" ? "sequential" : "parallel"}`, format);
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
