"use strict";

const pageSize = 50;

let current = null;
let forms = new Map();
let selected = null;
let showAll = false;

// ---------- Тести з пам'ятки ----------

async function loadSamples() {
    const samples = await (await fetch("/api/lab3/samples")).json();
    const box = el("div", { class: "sample-group" }, el("span", { class: "group-title" }, "Асоціативний закон"));
    for (const sample of samples)
        box.append(sampleChip(sample, sample.group, () => build(sample.expression)));
    $("samples").replaceChildren(box);
}

// ---------- Побудова ----------

async function build(expression) {
    $("expression").value = expression;
    history.replaceState(null, "", "#" + encodeURIComponent(expression));
    $("status").textContent = "Пошук еквівалентних форм…";
    current = await postJson("/api/factoring", { expression });
    forms = new Map(current.forms.map(f => [f.id, f]));
    showAll = false;
    render(current);
}

function render(r) {
    const o = r.original;
    show("result", true);
    const lr1Valid = o.lr1Errors.length === 0;
    const ok = r.forms.length > 0;

    const status = $("status");
    status.className = "card status " + (ok ? "ok" : "bad");
    status.textContent = !lr1Valid
        ? `Вираз не пройшов перевірку ЛР1: помилок ${o.lr1Errors.length}. Еквівалентні форми не будуються.`
        : !ok
            ? "Перевірка ЛР1: помилок немає. Але вираз містить ділення на 0."
            : `Перевірка ЛР1: помилок немає. Знайдено еквівалентних форм: ${r.forms.length} (разом з вихідною).`;

    renderLr1(o, build);
    show("zero-card", o.errors.length > 0);
    $("zero-errors").replaceChildren(...o.errors.map(e => el("li", {}, "терм ", el("code", {}, e.term), ": ", e.message)));

    for (const id of ["metrics", "summary", "selected-card", "table-card", "graph-card", "original-card"])
        show(id, ok);
    if (!ok) return;

    const start = forms.get(0), best = forms.get(r.bestId);
    renderMetrics(r, start, best);
    $("source-infix").textContent = o.expression;
    $("start-infix").textContent = start.infix;
    $("best-infix").textContent = `№${best.id}: ${best.infix}`;
    $("best-parallel").textContent = best.parallel;

    renderTable();
    renderGraph(r);
    $("original-svg").innerHTML = o.parallel.svg;
    $("original-tiers").textContent = `· ярусів: ${o.parallel.tiers}, операцій: ${o.parallel.operations}`;
    select(best.id, false);
}

function renderMetrics(r, start, best) {
    const metric = (name, before, after, hint) => el("div", { class: "metric" },
        el("div", { class: "name" }, name),
        el("div", { class: "value" }, String(before), el("span", { class: "arrow" }, "→"), el("span", { class: "after" }, String(after))),
        el("div", { class: "hint" }, hint));

    $("metrics").replaceChildren(
        el("div", { class: "metric" },
            el("div", { class: "name" }, "Еквівалентних форм"),
            el("div", { class: "value" }, String(r.forms.length)),
            el("div", { class: "hint" }, `переходів у графі: ${r.edgeCount}${r.truncated ? " · перебір обмежено" : ""}`)),
        metric("Ярусів", start.tiers, best.tiers, "форма №0 → найкраща"),
        metric("Операцій", start.operations, best.operations, "форма №0 → найкраща"),
        metric("Ширина", start.width, best.width, "макс. операцій на ярусі"));
}

// ---------- Таблиця форм ----------

function renderTable() {
    const r = current;
    $("table-title").textContent = `Множина еквівалентних форм (${r.forms.length})`;
    show("truncated-note", r.truncated);
    $("truncated-note").textContent = `Повний перебір дає понад ${r.fullSearchForms} форм, тому виконано обмежений перебір: ` +
        `на кожен наступний крок переходять ${r.beamWidth} найкращих форм (половина за ярусами, половина за операціями).`;

    const rows = showAll ? r.forms : r.forms.slice(0, pageSize);
    $("forms-body").replaceChildren(...rows.map(f => {
        const classes = [f.id === r.bestId ? "best" : "", f.id === 0 ? "original" : "", f.id === selected?.id ? "selected" : ""];
        return el("tr", { class: classes.join(" "), "data-id": f.id, onclick: () => select(f.id, true) },
            el("td", { class: "num" }, f.id),
            el("td", { class: "code" }, f.code),
            el("td", { class: "num" }, f.tiers),
            el("td", { class: "num" }, f.operations),
            el("td", { class: "num" }, f.width),
            el("td", { class: "infix" }, f.infix));
    }));

    const more = $("more");
    more.hidden = r.forms.length <= pageSize;
    more.textContent = showAll ? `Показати перші ${pageSize}` : `Показати всі ${r.forms.length}`;
}

function renderGraph(r) {
    $("graph-svg").innerHTML = r.graph;
    $("graph-info").textContent = r.graphNodes < r.forms.length
        ? `· показано ${r.graphNodes} з ${r.forms.length} форм: шляхи від форми №0 до найкращих`
        : `· ${r.forms.length} форм, ${r.edgeCount} переходів`;
    $("graph-svg").querySelectorAll(".form-node").forEach(node =>
        node.addEventListener("click", () => select(Number(node.dataset.id), true)));
}

// ---------- Вибрана форма ----------

function pathTo(form) {
    const steps = [];
    for (let f = form; f.parentId != null; f = forms.get(f.parentId))
        steps.push(f.step);
    return steps.reverse();
}

async function select(id, scroll) {
    const form = forms.get(id);
    selected = { id, tree: null };
    renderTable();

    const tree = await postJson("/api/parallel", { expression: form.infix });
    if (selected?.id !== id) return;
    selected.tree = tree;

    const label = id === current.bestId ? " — найкраща" : id === 0 ? " — вихідна" : "";
    $("selected-title").replaceChildren(`Форма №${id}${label} `,
        el("span", { class: "muted" }, `· код ${form.code} · ярусів: ${form.tiers}, операцій: ${form.operations}, ширина: ${form.width}`));

    const steps = pathTo(form);
    $("selected-path").replaceChildren(...(steps.length === 0
        ? [el("li", { class: "muted" }, "форма №0 — вираз після розкриття дужок і спрощення ЛР2, винесень немає")]
        : steps.map(s => el("li", {},
            el("span", { class: "badge factoring" }, `винесено ${s.factor}`), " ",
            el("code", {}, s.before), " = ", el("code", {}, s.after)))));

    $("selected-infix").textContent = form.infix;
    $("selected-parallel").textContent = form.parallel;
    $("selected-svg").innerHTML = tree.parallel?.svg ?? "";
    renderLevels("selected-levels", tree.parallel?.levels ?? []);
    if (scroll) $("selected-card").scrollIntoView({ behavior: "smooth", block: "start" });
}

// ---------- Збереження малюнків ----------

function saveImage(which, format) {
    if (which === "graph") saveSvg(current?.graph, "lr3-graph", format);
    else if (which === "original") saveSvg(current?.original.parallel?.svg, "lr3-form-0", format);
    else saveSvg(selected?.tree?.parallel?.svg, `lr3-form-${selected?.id}`, format);
}

// ---------- Старт ----------

$("form").addEventListener("submit", event => {
    event.preventDefault();
    const expression = $("expression").value.trim();
    if (expression) build(expression);
});

$("more").addEventListener("click", () => {
    showAll = !showAll;
    renderTable();
});

document.querySelectorAll("[data-download]").forEach(button =>
    button.addEventListener("click", () => saveImage(button.dataset.download, button.dataset.format)));

loadSamples();
const fromHash = decodeURIComponent(location.hash.slice(1));
build(fromHash || "a*b-2*a+b*c-c*2");
