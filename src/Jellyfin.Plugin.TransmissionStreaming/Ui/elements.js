import {help} from './ConfigurationPage?name=transmission-streaming-tooltips.js';

export const read = (value, name) => value?.[name] ?? value?.[name[0].toUpperCase() + name.slice(1)];

export function textElement(tag, className, text) {
    const element = document.createElement(tag);
    element.className = className;
    element.textContent = text;
    return element;
}

export function button(text, action, explanation) {
    // Jellyfin's bundled polyfill requires the legacy extension string and supplies the native button class.
    const element = document.createElement('button', 'emby-button');
    element.setAttribute('is', 'emby-button');
    element.type = 'button';
    element.classList.add('raised');
    buttonText(element, text, explanation);
    element.addEventListener('click', action);
    return element;
}

export function buttonText(element, text, explanation) {
    let label = element.querySelector('.ts-button-text');
    if (!label) {
        label = textElement('span', 'ts-button-text', '');
        element.replaceChildren(label);
    }
    label.textContent = text;
    help(label, explanation);
}
