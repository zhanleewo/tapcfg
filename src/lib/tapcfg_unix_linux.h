/**
 *  tapcfg - A cross-platform configuration utility for TAP driver
 *  Copyright (C) 2008-2009  Juho Vähä-Herttua
 *
 *  This library is free software; you can redistribute it and/or
 *  modify it under the terms of the GNU Lesser General Public
 *  License as published by the Free Software Foundation; either
 *  version 2.1 of the License, or (at your option) any later version.
 *
 *  This library is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 *  Lesser General Public License for more details.
 */

#include <linux/if_tun.h>

static int
tapcfg_start_dev(tapcfg_t *tapcfg, const char *ifname)
{
	int tap_fd = -1;
	struct ifreq ifr;
	int ret;

	/* Create a new tap device */
	tap_fd = open("/dev/net/tun", O_RDWR);
	if (tap_fd == -1) {
		taplog_log(TAPLOG_ERR,
		           "Error opening device /dev/net/tun: %s\n",
		           strerror(errno));
		taplog_log(TAPLOG_INFO,
		           "Check that you are running the program with "
		           "root privileges\n");
		return -1;
	}

	memset(&ifr, 0, sizeof(ifr));
	ifr.ifr_flags = IFF_TAP | IFF_NO_PI;
	if (ifname && strlen(ifname) < IFNAMSIZ) {
		strncpy(ifr.ifr_name, ifname, IFNAMSIZ-1);
	}
	ret = ioctl(tap_fd, TUNSETIFF, &ifr);

	if (ret == -1 && errno == EINVAL) {
		/* Try again without device name */
		memset(&ifr, 0, sizeof(ifr));
		ifr.ifr_flags = IFF_TAP | IFF_NO_PI;
		ret = ioctl(tap_fd, TUNSETIFF, &ifr);
	}
	if (ret == -1) {
		taplog_log(TAPLOG_ERR,
		           "Error setting the interface: %s\n",
		           strerror(errno));
		return -1;
	}

	/* Set the device name to be the one we got from OS */
	taplog_log(TAPLOG_DEBUG, "Device name %s\n", ifr.ifr_name);
	strncpy(tapcfg->ifname, ifr.ifr_name, sizeof(tapcfg->ifname)-1);

	/* Get the hardware address of the TAP interface */
	memset(&ifr, 0, sizeof(ifr));
	strcpy(ifr.ifr_name, tapcfg->ifname);
	ret = ioctl(tap_fd, SIOCGIFHWADDR, &ifr);
	if (ret == -1) {
		taplog_log(TAPLOG_ERR,
		           "Error getting the hardware address: %s\n",
		           strerror(errno));
		return -1;
	}
	memcpy(tapcfg->hwaddr, ifr.ifr_hwaddr.sa_data, HWADDRLEN);

	return tap_fd;
}

static void
tapcfg_iface_prepare(const char *ifname)
{
	/* No preparation needed on Linux */
}
