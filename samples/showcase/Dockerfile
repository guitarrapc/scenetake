# The tools that the showcase samples run (samples/showcase/*.yaml). record.sh builds this image and records each
# scenario in it with a Linux scenetake, so a maintainer needs none of these tools installed.
FROM debian:trixie-slim

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      bash bat ca-certificates fastfetch figlet git jq less lolcat nodejs npm nyancat sl toilet vim \
 && rm -rf /var/lib/apt/lists/* \
 && ln -s /usr/bin/batcat /usr/local/bin/bat

# oh-my-logo draws welcome.yaml's banner. A fixed version keeps the banner the same on every build.
RUN npm install --global --no-audit --no-fund oh-my-logo@0.5.0 \
 && npm cache clean --force

# less is git's pager, as on a desktop: without it git falls back to more, which counts color codes as width and
# wraps the log early. sl and lolcat install to /usr/games. TERM needs no setting: a PTY step gets xterm-256color.
ENV PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/games \
    LANG=C.UTF-8

RUN useradd --create-home demo \
 && install -d -o demo -g demo /home/demo/app
USER demo
WORKDIR /home/demo/app

# A small project for devtools.yaml: a source file for bat, a package.json for jq, and a git history with branches
# for git log. Fixed names, dates, and file modes give the same commit hashes on every build: a build from Windows
# copies every file as executable, which git would record.
COPY --chown=demo:demo app/ /home/demo/app/
RUN chmod 644 greet.py package.json && bash make-repo.sh && rm make-repo.sh
COPY --chown=demo:demo fastfetch.jsonc /home/demo/.config/fastfetch/config.jsonc
