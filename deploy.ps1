docker build -t vaxxybot:heroku -f Dockerfile.heroku .
docker tag vaxxybot:heroku registry.heroku.com/vaxxybot/web
docker push registry.heroku.com/vaxxybot/web
heroku container:release web -a vaxxybot
