.PHONY: all
all: out/Caefte.zip


########
# Logo #
########

build/img/logo.png: img/logo.png
	mkdir -p build/img
	cp $^ $@
	optipng -o7 $@

build/img/logo.ico: build/img/logo.png
	convert $< -resize 256x256 -define icon:auto-resize="256,128,64,32,16" $@

build/dist/favicon.ico: build/img/logo.ico
	cp $< $@


#######
# Elm #
#######

ELM_SOURCES=$(wildcard frontend/elm/*.elm)
ELM_API=build/api/result/Api.elm

build/locked: frontend/package.json
	cd frontend && (pnpm install || npm install) && cd .. && touch $@

${ELM_API}: build/api/bin/Debug/API.exe build/bin/API/Caefte.exe
	mono $^ $@

build/dist/index.html: frontend/index.html frontend/index.js ${ELM_SOURCES} ${ELM_API} frontend/elm.json build/locked
	cd frontend && parcel build -d ../build/dist --no-source-maps --experimental-scope-hoisting --no-content-hash --public-url . ../$<

# The above command actually generates both files, but make gets confused and tries to do it twice in parallel, hence the following
build/dist/frontend.e31bb0bc.js: build/dist/index.html

######
# C# #
######

CS_SOURCES=build/Caefte.sln build/Caefte.csproj build/img/logo.ico.gz $(wildcard backend/*.cs) $(wildcard backend/*/*.cs)
CS_API=build/api/result/Controller.cs
API_SOURCES=build/api/API.sln build/api/API.csproj $(wildcard build/api/src/*.cs)

build/bin/API/Caefte.exe: ${CS_SOURCES}
	msbuild -p:Configuration=API $<

build/api/bin/Debug/API.exe: ${API_SOURCES}
	msbuild $<

${CS_API}: build/api/bin/Debug/API.exe build/bin/API/Caefte.exe
	mono $^ $@

build/bin/Debug/Caefte.exe: ${CS_SOURCES} ${CS_API} build/dist/favicon.ico
	msbuild $<

build/bin/Release/Caefte.exe: ${CS_SOURCES} ${CS_API} build/dist/index.html.gz build/dist/frontend.e31bb0bc.js.gz
	msbuild -p:Configuration=Release $<


##########
# Output #
##########

out/Caefte.zip: build/bin/Release/Caefte.exe
	cd build/bin/Release && zip -9 ../../../$@ Caefte.exe

out/source.zip: $(shell git ls-files)
	git archive HEAD -o $@ --format=zip


########
# Misc #
########

%.gz: %
	pigz -9 <$^ > $@

.PHONY: run
run: build/bin/Debug/Caefte.exe
	tmux new-session "mono $^; read trash" \; split-window -h "cd frontend && parcel watch -d ../build/dist index.html; read trash" \; attach

.PHONY: clean
clean:
	rm -rf build/img build/elm-stuff build/bin build/obj build/dist out/source.zip out/Caefte.zip frontend/node_modules frontend/.cache frontend/elm-stuff
