# The tools that the showcase samples run (samples/showcase/*.yaml). record.sh builds this image and records each
# scenario in it with a Linux scenetake, so a maintainer needs none of these tools installed.
FROM debian:trixie-slim

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      bash bat ca-certificates fastfetch figlet git jq less lolcat nyancat sl toilet \
 && rm -rf /var/lib/apt/lists/* \
 && ln -s /usr/bin/batcat /usr/local/bin/bat

# less is git's pager, as on a desktop: without it git falls back to more, which counts color codes as width and
# wraps the log early. sl and lolcat install to /usr/games. scenetake does not set TERM for PTY steps, and a container has none, so curses
# tools such as sl would not start without it.
ENV PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/games \
    TERM=xterm-256color \
    LANG=C.UTF-8

RUN useradd --create-home demo \
 && install -d -o demo -g demo /home/demo/app
USER demo
WORKDIR /home/demo/app

# A small project for devtools.yaml: a source file for bat, a package.json for jq, and a git history with branches
# for git log. Fixed names and dates give the same commit hashes on every build.
COPY --chown=demo:demo app/ /home/demo/app/
RUN bash make-repo.sh && rm make-repo.sh
COPY --chown=demo:demo fastfetch.jsonc /home/demo/.config/fastfetch/config.jsonc
