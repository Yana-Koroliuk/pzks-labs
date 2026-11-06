"use strict";

// Спільне для сторінок ЛР2 і ЛР3.

const $ = id => document.getElementById(id);

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

async function postJson(url, body) {
    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
    });
    return response.json();
}

function sampleChip(sample, group, onclick) {
    return el("button", {
        type: "button",
        class: `chip ${group}`,
        title: `${sample.description ? sample.description + ": " : ""}${sample.expression}`,
        onclick,
    }, sample.id);
}

/** Помилки ЛР1 у картці #lr1: вираз з підсвіченими помилками, список і автовиправлення. */
function renderLr1(r, onUseCorrection) {
    const valid = r.lr1Errors.length === 0;
    show("lr1", !valid);
    if (valid) return;

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
            el("button", { type: "button", class: "small", onclick: () => onUseCorrection(r.correction.expression) }, "Використати")));
    }
}

/** Таблиця «операції по ярусах» дерева паралельної форми. */
function renderLevels(tableId, levels) {
    $(tableId).replaceChildren(...levels.map((level, i) =>
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

function saveSvg(svg, name, format) {
    if (!svg) return;
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
