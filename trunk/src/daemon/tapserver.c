
#include <stdlib.h>
#include <assert.h>
#include <string.h>
#include <unistd.h>
#include <errno.h>

#include <stdio.h>

#include "tapserver.h"

#if defined(_WIN32) || defined(_WIN64)

#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>

typedef HANDLE thread_handle_t;

#define THREAD_RETVAL DWORD WINAPI
#define THREAD_CREATE(handle, func, arg) \
	handle = CreateThread(NULL, 0, func, arg, 0, NULL)
#define THREAD_JOIN(handle) WaitForSingleObject(handle, INFINITE); CloseHandle(handle)

typedef HANDLE mutex_handle_t;

#define MUTEX_CREATE(handle) handle = CreateMutex(NULL, FALSE, NULL)
#define MUTEX_LOCK(handle) WaitForSingleObject(handle, INFINITE)
#define MUTEX_UNLOCK(handle) ReleaseMutex(handle)
#define MUTEX_DESTROY(handle) CloseHandle(handle)

#else /* Use pthread library and POSIX headers */

#include <pthread.h>
#include <netinet/in.h>

typedef pthread_t thread_handle_t;

#define THREAD_RETVAL void *
#define THREAD_CREATE(handle, func, arg) \
	if (pthread_create(&(handle), NULL, func, arg)) handle = 0
#define THREAD_JOIN(handle) pthread_join(handle, NULL)

typedef pthread_mutex_t mutex_handle_t;

#define MUTEX_CREATE(handle) pthread_mutex_init(&(handle), NULL)
#define MUTEX_LOCK(handle) pthread_mutex_lock(&(handle))
#define MUTEX_UNLOCK(handle) pthread_mutex_unlock(&(handle))
#define MUTEX_DESTROY(handle) pthread_mutex_destroy(&(handle))
#endif

#include "create_server.h"

#define MAX_CLIENTS 5

struct tapserver_s {
	int server_fd;
	unsigned short server_port;

	int running;
	mutex_handle_t run_mutex;

	int max_clients;
	tapcfg_t *tapcfg;
	int waitms;

	int clients;
	int clienttab[MAX_CLIENTS];
	mutex_handle_t mutex;

	thread_handle_t reader;
	thread_handle_t writer;
};


tapserver_t *
tapserver_init(tapcfg_t *tapcfg, int waitms)
{
	tapserver_t *ret;

	ret = calloc(1, sizeof(tapserver_t));
	ret->max_clients = MAX_CLIENTS;
	ret->tapcfg = tapcfg;
	ret->waitms = waitms;
	MUTEX_CREATE(ret->run_mutex);
	MUTEX_CREATE(ret->mutex);

	return ret;
}

void
tapserver_destroy(tapserver_t *server)
{
	MUTEX_DESTROY(server->mutex);
	MUTEX_DESTROY(server->run_mutex);
	free(server);
}

int
tapserver_add_client(tapserver_t *server, int fd)
{
	MUTEX_LOCK(server->mutex);
	if (server->clients >= server->max_clients) {
		MUTEX_UNLOCK(server->mutex);
		return -1;
	}
	server->clienttab[server->clients] = fd;
	server->clients++;
	MUTEX_UNLOCK(server->mutex);
}

static void
remove_client(tapserver_t *server, int idx)
{
	assert(server);
	assert(idx < server->clients);

	if (idx < (server->clients-1)) {
		memmove(&server->clienttab[idx],
			&server->clienttab[idx+1],
			sizeof(int) *
			(server->clients - idx - 1));
	}
	server->clients--;
}

static THREAD_RETVAL
reader_thread(void *arg)
{
	tapserver_t *server = arg;
	tapcfg_t *tapcfg = server->tapcfg;
	unsigned char buf[4096];
	int running;
	int i, tmp;

	assert(server);

	/* If we don't have tapcfg, finish the thread */
	if (!tapcfg) {
		return 0;
	}

	printf("Starting reader thread\n");

	do {
		while (tapcfg_wait_readable(tapcfg, server->waitms)) {
			int len;

			len = tapcfg_read(tapcfg, buf, sizeof(buf));
			if (len <= 0) {
				/* XXX: We could quite more nicely */
				break;
			}
			printf("Read %d bytes from the device\n", len);

			MUTEX_LOCK(server->mutex);
			for (i=0; i<server->clients; i++) { 
				unsigned char sizebuf[2];

				sizebuf[0] = (len >> 8) & 0xff;
				sizebuf[1] = len & 0xff;

				/* Write received data length */
				tmp = write(server->clienttab[i], sizebuf, 2);
				if (tmp > 0) {
					/* Write received data */
					tmp = write(server->clienttab[i], buf, len);
				}

				if (tmp <= 0) {
					remove_client(server, i);
				}
				printf("Wrote %d bytes to the client\n", len);
			}
			MUTEX_UNLOCK(server->mutex);
		}

		MUTEX_LOCK(server->run_mutex);
		running = server->running;
		MUTEX_UNLOCK(server->run_mutex);
	} while (running);

	printf("Stopping reader thread\n");

	return 0;
}

static THREAD_RETVAL
writer_thread(void *arg)
{
	tapserver_t *server = arg;
	tapcfg_t *tapcfg = server->tapcfg;
	unsigned char buf[4096];
	int running;
	int i, j, tmp;

	assert(server);

	printf("Starting writer thread\n");

	do {
		fd_set rfds;
		struct timeval tv;
		int highest_fd = -1;

		FD_ZERO(&rfds);

		MUTEX_LOCK(server->mutex);
		if (server->clients < server->max_clients) {
			FD_SET(server->server_fd, &rfds);
			highest_fd = server->server_fd;
		}
		for (i=0; i<server->clients; i++) {
			FD_SET(server->clienttab[i], &rfds);
			if (server->clienttab[i] > highest_fd) {
				highest_fd = server->clienttab[i];
			}
		}
		MUTEX_UNLOCK(server->mutex);

		tv.tv_sec = server->waitms / 1000;
		tv.tv_usec = server->waitms % 1000;
		tmp = select(highest_fd+1, &rfds, NULL, NULL, &tv);

		MUTEX_LOCK(server->mutex);
		for (i=0; i<server->clients; i++) {
			unsigned char sizebuf[2];
			int len;

			if (!FD_ISSET(server->clienttab[i], &rfds))
				continue;

			tmp = read(server->clienttab[i], sizebuf, 2);
			if (tmp > 0) {
				len = (sizebuf[0]&0xff) << 8 | sizebuf[1];
				if (len <= sizeof(buf)) {
					tmp = read(server->clienttab[i], buf, len);
				} else {
					/* XXX: Buffer size error handled as read error */
					tmp = -1;
				}
			}
			if (tmp <= 0) {
				remove_client(server, i);
				continue;
			}
			printf("Read %d bytes from the client\n", len);

			if (tapcfg) {
				tmp = tapcfg_write(tapcfg, buf, len);
				if (tmp <= 0) {
					MUTEX_LOCK(server->run_mutex);
					server->running = 0;
					MUTEX_UNLOCK(server->run_mutex);
					MUTEX_UNLOCK(server->mutex);
					goto exit;
				}
				printf("Wrote %d bytes to the device\n", len);
			} else {
				for (j=0; j<server->clients; j++) {
					if (i == j) {
						continue;
					}

					tmp = write(server->clienttab[j], sizebuf, 2);
					if (tmp > 0) {
						tmp = write(server->clienttab[j], buf, len);
					}
					if (tmp <= 0) {
						remove_client(server, j);
					}
					printf("Wrote %d bytes to the client\n", len);
				}
			}
		}
		MUTEX_UNLOCK(server->mutex);

		/* Accept a client and add it to the client table */
		if (FD_ISSET(server->server_fd, &rfds)) {
			/* FIXME: This doesn't support IPv6 */
			struct sockaddr_in caddr;
			socklen_t caddr_size;
			int client_fd;

			printf("Accepting a new client\n");
			caddr_size = sizeof(caddr);
			client_fd = accept(server->server_fd,
					   (struct sockaddr *) &caddr,
					   &caddr_size);
			if (client_fd == -1) {
				/* XXX: This error should definitely be reported */
				goto exit;
			}
			printf("Accepted a new client\n");

			tapserver_add_client(server, client_fd);
		}

		MUTEX_LOCK(server->run_mutex);
		running = server->running;
		MUTEX_UNLOCK(server->run_mutex);
	} while (running);

exit:
	printf("Stopping writer thread\n");

	return 0;
}

void
tapserver_start(tapserver_t *server)
{
	unsigned short port = 1234;

	server->server_fd = create_server(&port, 0, 1);
	server->running = 1;

	THREAD_CREATE(server->reader, reader_thread, server);
	THREAD_CREATE(server->writer, writer_thread, server);
}

void
tapserver_stop(tapserver_t *server)
{
	MUTEX_LOCK(server->run_mutex);
	server->running = 0;
	MUTEX_UNLOCK(server->run_mutex);

	THREAD_JOIN(server->reader);
	THREAD_JOIN(server->writer);
}

