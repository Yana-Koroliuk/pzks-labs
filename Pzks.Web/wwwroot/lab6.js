"use strict";

// Сторінка ЛР6: вибір оптимальної форми виразу для VLIW-системи.
const defaults = { processors: 2, banks: 1, add: 2, subtract: 3, multiply: 4, divide: 8, memory: 1 };
const paramIds = Object.keys(defaults);
const defaultExpression = "(a-c)*k+(b-c)*t-(a-t)*x";
const pageSize = 30;

let current = null;
let shownRows = pageSize;

const fmt = value => Number(value).toFixed(2);
const td = (value, cls = "num") => el("td", { class: cls }, value);

function plural(n, one, few, many) {
    const last = n % 10, lastTwo = n % 100;
    const word = last === 1 && lastTwo !== 11 ? one
        : last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14) ? few : many;
    return `${n} ${word}`;
}

const tacts = n => plural(n, "такт", "такти", "тактів");
const forms = n => plural(n, "форма", "форми", "форм");

// ---------- Параметри ----------

function readParams() {
    const params = {};
    for (const id of paramIds) {
        const value = parseInt($("p-" + id).value, 10);
        params[id] = Number.isNaN(value) ? defaults[id] : value;
    }
    return params;
}

function writeParams(params) {
    for (const id of paramIds)
        $("p-" + id).value = params[id];
}

// ---------- Тести ----------

async function loadSamples() {
    const groups = [
        ["/api/lab6/samples", "Дослідження ЛР6"],
        ["/api/lab3/samples", "Тести ЛР3"],
        ["/api/lab4/samples", "Тести ЛР4"],
    ];
    const boxes = [];
    for (const [url, title] of groups) {
        const samples = await (await fetch(url)).json();
        const box = el("div", { class: "sample-group" }, el("span", { class: "group-title" }, title));
        for (const sample of samples)
            box.append(sampleChip(sample, sample.group, () => build(sample.expression)));
        boxes.push(box);
    }
    $("samples").replaceChildren(...boxes);
}

// ---------- Побудова ----------

async function build(expression) {
    $("expression").value = expression;
    history.replaceState(null, "", "#" + encodeURIComponent(expression));
    $("status").className = "card status";
    $("status").textContent = "Моделювання всіх форм…";
    show("result", true);
    current = await postJson("/api/lab6/optimize", { expression, ...readParams() });
    shownRows = pageSize;
    render(current);
}

function render(r) {
    const p = r.original;
    const lr1Valid = p.lr1Errors.length === 0;
    const ok = r.valid;

    const status = $("status");
    status.className = "card status " + (ok ? "ok" : "bad");
    status.textContent = !lr1Valid
        ? `Вираз не пройшов перевірку ЛР1: помилок ${p.lr1Errors.length}. Форми не будуються.`
        : p.errors.length > 0 ? "Перевірка ЛР1: помилок немає. Але вираз містить ділення на 0, тому дерево ЛР2 не будується."
        : !ok ? "Моделювання неможливе."
        : statusText(r);

    renderLr1(p, build);
    renderProblems(r);

    for (const id of ["metrics", "optimal-card", "compare-card", "criteria-card", "runs-card", "distribution-card",
        "directed-card", "study-card", "graph-card"])
        show(id, ok);
    if (!ok) return;

    renderMetrics(r);
    renderOptimal(r);
    renderCompare(r);
    renderCriteria(r);
    renderRuns(r);
    renderDistribution(r);
    renderDirected(r);
    renderStudy(r);
    $("graph-svg").innerHTML = r.optimalPictures.graph;
}

function statusText(r) {
    const o = r.originalRun, best = r.optimal;
    const gain = o.parallelTime - best.parallelTime;
    return `Перевірка ЛР1: помилок немає. Промодельовано форм: ${r.formCount}. `
        + (gain > 0
            ? `Оптимальна форма виконується за ${tacts(best.parallelTime)} замість ${o.parallelTime} (на ${tacts(gain)} швидше).`
            : `Вихідна форма вже оптимальна: Tp = ${o.parallelTime}.`)
        + (r.optimalCount > 1 ? ` Оптимальних форм з таким Tp: ${r.optimalCount}.` : "");
}

function renderProblems(r) {
    const p = r.original;
    let title = "", items = [];
    if (p.lr1Errors.length === 0 && p.errors.length > 0) {
        title = "Недопустимі операції: ділення на 0";
        items = p.errors.map(e => `терм ${e.term}: ${e.message}`);
    } else if (r.errors.length > 0) {
        title = "Моделювання неможливе";
        items = r.errors;
    }
    show("problem-card", items.length > 0);
    $("problem-title").textContent = title;
    $("problem-list").replaceChildren(...items.map(text => el("li", {}, text)));
}

function renderMetrics(r) {
    const o = r.originalRun, best = r.optimal, p = r.parameters;
    const gain = o.parallelTime === 0 ? 0 : (o.parallelTime - best.parallelTime) / o.parallelTime * 100;
    const metric = (name, value, hint) => el("div", { class: "metric" },
        el("div", { class: "name" }, name), el("div", { class: "value" }, value), el("div", { class: "hint" }, hint));
    const change = (before, after) => {
        const value = el("span");
        value.append(String(before), el("span", { class: "arrow" }, "→"), el("span", { class: "after" }, String(after)));
        return value;
    };
    $("metrics").replaceChildren(
        metric("Еквівалентних форм", r.formCount, r.truncated ? `перебір обмежено (більше ${r.fullSearchForms})` : "повний перебір"),
        metric("Tp", change(o.parallelTime, best.parallelTime), `тактів на ${p.processors} процесорах`),
        metric("Виграш у часі", `${gain.toFixed(1)}%`, `на ${tacts(o.parallelTime - best.parallelTime)}`),
        metric("T1", change(o.sequential, best.sequential), "тактів на 1 процесорі"),
        metric("Ky", change(fmt(o.speedup), fmt(best.speedup)), "Ky = T1 / Tp"),
        metric("E", change(fmt(o.efficiency), fmt(best.efficiency)), `E = Ky / ${p.processors}`));
}

function renderOptimal(r) {
    const best = r.optimal, pictures = r.optimalPictures;
    $("optimal-info").textContent = `· ярусів: ${best.tiers}, операцій: ${best.operations}, ширина: ${best.width}, Tp = ${best.parallelTime}`;
    $("optimal-infix").textContent = best.infix;
    $("optimal-parallel").textContent = best.parallel;
    $("optimal-code").textContent = best.code;
    $("optimal-path").replaceChildren(...pictures.path.map(step => el("li", {},
        el("span", { class: `badge ${step.law === "D" ? "distributive" : "factoring"}` }, step.law === "D" ? `D ${step.factor}` : `A ${step.factor}`),
        el("code", {}, step.before), " = ", el("code", {}, step.after))));
    const notes = [];
    if (r.directedIsBetter)
        notes.push("Цю форму знайшов направлений пошук: повний перебір обмежено, і серед його форм такої швидкої немає.");
    else if (best.id === 0)
        notes.push("Перетворення не зменшують час: вихідна форма вже оптимальна для цієї системи.");
    if (r.optimalCount > 1)
        notes.push(`Такий самий Tp = ${best.parallelTime} мають ${forms(r.optimalCount)} (у таблиці форм виділені зеленим). `
            + "Тут показано ту, що має найменше операцій, далі ярусів і перетворень.");
    const note = $("optimal-note");
    note.hidden = notes.length === 0;
    note.textContent = notes.join(" ");
    $("optimal-tree").innerHTML = pictures.tree;
}

function runRow(label, run) {
    return el("tr", { class: label === "Оптимальна" ? "best" : "" },
        td(label, ""), td(run.tiers), td(run.operations), td(run.width), td(run.initialTasks),
        td(run.sequential), td(run.parallelTime), td(fmt(run.speedup)), td(fmt(run.efficiency)), td(run.memoryWaits));
}

function renderCompare(r) {
    $("compare-body").replaceChildren(runRow("Вихідна (№0)", r.originalRun), runRow("Оптимальна", r.optimal));
    $("original-gantt").innerHTML = r.originalPictures.gantt;
    $("optimal-gantt").innerHTML = r.optimalPictures.gantt;
}

function renderCriteria(r) {
    const rows = [
        ["Найменший Tp (обрано)", r.optimal],
        ["Найменше ярусів (критерій ЛР3–ЛР4)", r.byTiers],
        ["Найбільше Ky", r.maxSpeedup],
        ["Найбільше E", r.maxEfficiency],
        ["Вихідна форма", r.originalRun],
    ];
    $("criteria-body").replaceChildren(...rows.map(([label, run], i) => el("tr", { class: i === 0 ? "best" : "" },
        td(label, ""), td(run.code, "code"), td(run.infix, "infix"), td(run.tiers), td(run.operations),
        td(run.sequential), td(run.parallelTime), td(fmt(run.speedup)), td(fmt(run.efficiency)))));
}

function renderRuns(r) {
    const rows = r.runs.slice(0, shownRows);
    if (shownRows < r.runs.length && !rows.some(run => run.id === 0))
        rows.push(r.originalRun);
    const optimal = r.directedIsBetter ? -1 : r.optimal.parallelTime;
    $("runs-info").textContent = `· усього ${r.formCount}, за зростанням Tp`
        + (r.directedIsBetter ? "" : `, оптимальних (Tp = ${optimal}): ${r.optimalCount}`);
    $("runs-body").replaceChildren(...rows.map(run => el("tr", {
        class: [run.parallelTime === optimal ? "best" : "", run.id === 0 ? "original" : ""].join(" ").trim(),
    }, td(run.rank), td(run.id), td(run.code, "code"), td(run.infix, "infix"), td(run.tiers), td(run.operations),
        td(run.width), td(run.initialTasks), td(run.sequential), td(run.parallelTime), td(fmt(run.speedup)),
        td(fmt(run.efficiency)), td(run.memoryWaits))));

    const more = $("runs-more");
    more.hidden = shownRows >= r.runs.length;
    more.textContent = `Показати ще (${Math.min(pageSize, r.runs.length - shownRows)})`;
}

function renderDistribution(r) {
    const max = Math.max(...r.distribution.map(d => d.forms));
    const optimal = r.optimal.parallelTime, original = r.originalRun.parallelTime;
    $("distribution").replaceChildren(...r.distribution.map(d => el("div", { class: "bar-row" },
        el("span", { class: "bar-label" }, `Tp = ${d.parallelTime}`),
        el("span", { class: "bar-track" }, el("span", {
            class: "bar" + (d.parallelTime === optimal ? " best" : d.parallelTime === original ? " original" : ""),
            style: `width: ${Math.max(2, d.forms / max * 100)}%`,
        })),
        el("span", { class: "bar-value" }, `${d.forms}` + (d.parallelTime === original ? " · вихідна" : "")))));
}

function renderDirected(r) {
    const d = r.directed;
    $("directed-info").textContent = `· на крок переходять ${d.beamWidth} форм, зупинка після ${d.patience} кроків без покращення`;
    const full = r.best.parallelTime;
    const verdict = d.best.parallelTime < full ? `швидше за повний перебір (${full})`
        : d.best.parallelTime === full ? "той самий час, що й повний перебір" : `повний перебір дав ${full}`;
    const line = (label, value) => el("div", { class: "form-row" }, el("span", { class: "label" }, label), value);
    $("directed-summary").replaceChildren(
        line("Промодельовано форм", el("span", {}, `${d.evaluated} (повний перебір: ${r.formCount})`)),
        line("Найкраща знайдена форма", el("code", {}, d.best.infix)),
        line("Tp", el("span", {}, `${d.best.parallelTime}: ${verdict}`)));
    $("directed-body").replaceChildren(...d.levels.map(level => el("tr", {},
        td(level.depth), td(level.evaluated), td(level.bestTime))));
}

function renderStudy(r) {
    const p = r.parameters;
    $("study-body").replaceChildren(...r.study.map(s => {
        const same = s.parameters.processors === p.processors && s.parameters.banks === p.banks;
        const diff = s.chosen.parallelTime - s.best.parallelTime;
        return el("tr", { class: same ? "best" : "" },
            td(s.label, ""), td(s.best.code, "code"), td(s.best.infix, "infix"), td(s.best.parallelTime),
            td(fmt(s.best.speedup)), td(fmt(s.best.efficiency)), td(s.chosen.parallelTime), td(diff === 0 ? "0" : `+${diff}`));
    }));
}

// ---------- Збереження таблиці і малюнків ----------

function saveRunsCsv() {
    if (!current?.valid) return;
    const header = ["Місце", "№", "Код", "Форма", "Паралельна форма", "Ярусів", "Операцій", "Ширина", "Поч. задач",
        "T1", "Tp", "Ky", "E", "Очікування", "Оптимальна"];
    const optimal = current.directedIsBetter ? -1 : current.optimal.parallelTime;
    const quote = value => `"${String(value).replaceAll('"', '""')}"`;
    const lines = current.runs.map(run => [run.rank, run.id, run.code, run.infix, run.parallel, run.tiers, run.operations,
        run.width, run.initialTasks, run.sequential, run.parallelTime, fmt(run.speedup), fmt(run.efficiency),
        run.memoryWaits, run.parallelTime === optimal ? "так" : ""].map(quote).join(";"));
    const csv = "\uFEFF" + [header.map(quote).join(";"), ...lines].join("\r\n");
    download(new Blob([csv], { type: "text/csv;charset=utf-8" }), "lr6-forms.csv");
}

function saveImage(which, format) {
    const pictures = current?.optimalPictures;
    if (!pictures) return;
    const name = `lr6-${which}`;
    if (which === "tree") saveSvg(pictures.tree, name, format);
    else if (which === "graph") saveSvg(pictures.graph, name, format);
    else saveSvg(pictures.gantt, name, format);
}

// ---------- Старт ----------

$("form").addEventListener("submit", event => {
    event.preventDefault();
    const expression = $("expression").value.trim();
    if (expression) build(expression);
});

for (const id of paramIds)
    $("p-" + id).addEventListener("change", () => {
        const expression = $("expression").value.trim();
        if (expression) build(expression);
    });

$("reset").addEventListener("click", () => {
    writeParams(defaults);
    const expression = $("expression").value.trim();
    if (expression) build(expression);
});

$("runs-more").addEventListener("click", () => {
    shownRows += pageSize;
    renderRuns(current);
});

$("runs-csv").addEventListener("click", saveRunsCsv);

document.querySelectorAll("[data-download]").forEach(button =>
    button.addEventListener("click", () => saveImage(button.dataset.download, button.dataset.format)));

loadSamples();
const fromHash = decodeURIComponent(location.hash.slice(1));
build(fromHash || defaultExpression);
