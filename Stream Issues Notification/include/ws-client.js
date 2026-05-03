const sbClient = new StreamerbotClient({
    host: '127.0.0.1',
    port: 8080,
    onConnect: (data) => {
        console.log('Connected!');
        let errorBox = document.getElementById('error-message');
        if (errorBox !== null) {
            document.body.removeChild(errorBox);
        }
    },
    onDisconnect: () => {
        console.log('Disconnected socket!');
        if (document.getElementById('error-message') === null) {
            let d = document.createElement('div');
            d.className = 'error-message';
            d.id = 'error-message';
            d.textContent = 'No socket connection!';
            document.body.appendChild(d)
        }
    }
});

export { sbClient };
